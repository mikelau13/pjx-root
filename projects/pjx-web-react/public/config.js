// Runtime configuration. Overwritten in deployed environments by a ConfigMap
// mounted at /usr/share/nginx/html/config.js — see helm-pjx/templates.
window.__PJX_CONFIG__ = {
  GRAPHQL_ENDPOINT:  "https://ql.pjx.test",
  SSO_ISSUER_URL:    "https://sso.pjx.test",
  SSO_CLIENT_ID:     "pjx-web-react",
  API_DOTNET_URL:    "https://api.pjx.test",
  PUBLIC_URL:        "https://pjx.test"
};
