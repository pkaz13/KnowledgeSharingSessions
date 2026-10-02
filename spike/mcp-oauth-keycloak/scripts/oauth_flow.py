"""Walk the MCP OAuth flow the way a client does: 401 -> PRM -> AS metadata -> PKCE auth code -> token -> tools/call.

Logs in through the Keycloak HTML form, so no browser is needed. Usage:
    python3 oauth_flow.py [client_id] [redirect_uri]
"""
import base64, hashlib, http.cookiejar, json, re, secrets, sys, urllib.error, urllib.parse, urllib.request

MCP_URL = "http://localhost:5080/mcp"
CLIENT_ID = sys.argv[1] if len(sys.argv) > 1 else "vscode"
REDIRECT_URI = sys.argv[2] if len(sys.argv) > 2 else "http://127.0.0.1:33418/"
SCOPE = sys.argv[3] if len(sys.argv) > 3 else None


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar), NoRedirect)


def call(url, data=None, headers=None):
    req = urllib.request.Request(url, data=data, headers=headers or {})
    try:
        with opener.open(req) as r:
            return r.status, dict(r.headers), r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, dict(e.headers), e.read().decode()


def mcp(token=None):
    body = json.dumps({"jsonrpc": "2.0", "id": 1, "method": "tools/call", "params": {"name": "whoami", "arguments": {}}}).encode()
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    return call(MCP_URL, body, headers)


# 1. Unauthenticated call -> 401 with resource_metadata
status, headers, _ = mcp()
challenge = headers.get("WWW-Authenticate", "")
print("1. MCP without token:", status, challenge)
prm_url = re.search(r'resource_metadata="([^"]+)"', challenge).group(1)

# 2. Protected Resource Metadata
prm = json.loads(call(prm_url)[2])
print("2. PRM:", prm)
issuer = prm["authorization_servers"][0]

# 3. AS metadata (OIDC discovery)
meta = json.loads(call(f"{issuer}/.well-known/openid-configuration")[2])
assert meta["issuer"] == issuer, "issuer mismatch"
assert "S256" in meta["code_challenge_methods_supported"]
print("3. AS metadata OK, issuer:", meta["issuer"])

# 4. Authorization request with PKCE + RFC 8707 resource
verifier = secrets.token_urlsafe(48)
challenge_s256 = base64.urlsafe_b64encode(hashlib.sha256(verifier.encode()).digest()).rstrip(b"=").decode()
state = secrets.token_urlsafe(8)
auth_url = meta["authorization_endpoint"] + "?" + urllib.parse.urlencode({
    "response_type": "code", "client_id": CLIENT_ID, "redirect_uri": REDIRECT_URI,
    "scope": SCOPE or " ".join(prm["scopes_supported"]), "state": state,
    "code_challenge": challenge_s256, "code_challenge_method": "S256", "resource": prm["resource"],
})
status, _, html = call(auth_url)
print("4. Authorize page:", status)
for c in jar: c.secure = False  # browsers treat localhost as secure; urllib does not
form_action = re.search(r'action="([^"]+)"', html)
if not form_action:
    print(html[:2000])
    sys.exit(1)
login_url = form_action.group(1).replace("&amp;", "&")

# 5. Log in as alice -> redirect with code
status, headers, _body = call(login_url, urllib.parse.urlencode({"username": "alice", "password": "alice"}).encode(),
                          {"Content-Type": "application/x-www-form-urlencoded"})
location = headers.get("Location", "")
if status != 302: print([c.name for c in jar], _body); sys.exit(1)
print("5. Login:", status, location)
query = urllib.parse.parse_qs(urllib.parse.urlparse(location).query)
assert query["state"][0] == state
assert query.get("iss", [None])[0] == issuer, "RFC 9207 iss missing or wrong"
code = query["code"][0]

# 6. Token request with verifier + resource
status, _, body = call(meta["token_endpoint"], urllib.parse.urlencode({
    "grant_type": "authorization_code", "client_id": CLIENT_ID, "code": code, "redirect_uri": REDIRECT_URI,
    "code_verifier": verifier, "resource": prm["resource"],
}).encode(), {"Content-Type": "application/x-www-form-urlencoded"})
print("6. Token:", status, "" if status == 200 else body)
token = json.loads(body)["access_token"]
payload = json.loads(base64.urlsafe_b64decode(token.split(".")[1] + "=="))
print("   claims:", {k: payload.get(k) for k in ["iss", "aud", "azp", "scope", "preferred_username"]})

# 7. MCP call with token
status, _, body = mcp(token)
print("7. MCP with token:", status, body.strip()[:400])
