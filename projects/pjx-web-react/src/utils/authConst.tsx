import { config } from './runtimeConfig';

// Read from runtime configuration, not build-time substitution. In a deployed
// environment public/config.js is replaced by a ConfigMap mounted over
// /usr/share/nginx/html/config.js, so these URLs change without rebuilding the
// image — see docs/architecture-upgrade/phase-10-deployable.md Step 3.
const issuer = config.ssoIssuerUrl;
const publicUrl = config.publicUrl;

export const IDENTITY_CONFIG = {
    authority: issuer, //(string): The URL of the OIDC provider.
    client_id: config.ssoClientId, //(string): Your client application's identifier as registered with the OIDC provider.
    // The three redirect URIs are DERIVED from publicUrl rather than configured
    // separately. They must match Config.cs on the SSO side exactly, and five
    // independent values is five chances to typo one.
    redirect_uri: `${publicUrl}/signin-oidc`, //The URI of your client application to receive a response from the OIDC provider.
    login: `${issuer}/login`,
    profile: `${issuer}/profile`,
    automaticSilentRenew: false, //(boolean, default: false): Flag to indicate if there should be an automatic attempt to renew the access token prior to its expiration.
    loadUserInfo: false, //(boolean, default: true): Flag to control if additional identity data is loaded from the user info endpoint in order to populate the user's profile.
    silent_redirect_uri: `${publicUrl}/silentrenew`, //(string): The URL for the page containing the code handling the silent renew.
    post_logout_redirect_uri: `${publicUrl}/logout/callback`, // (string): The OIDC post-logout redirect URI.
    audience: "https://www.audience.com", //is there a way to specific the audience when making the jwt
    response_type: "code", //(string, default: 'id_token'): The type of response desired from the OIDC provider.
    grantType: "password",
    scope: "openid profile web_sso", //(string, default: 'openid'): The scope being requested from the OIDC provider.
    webAuthResponseType: "code",
    registration_url: `${issuer}/api/register`,
    activation_url: `${issuer}/api/validate`,
};

export const METADATA_OIDC = {
    issuer: issuer,
    jwks_uri: `${issuer}/.well-known/openid-configuration/jwks`,
    authorization_endpoint: `${issuer}/connect/authorize`,
    token_endpoint: `${issuer}/connect/token`,
    userinfo_endpoint: `${issuer}/connect/userinfo`,
    end_session_endpoint: `${issuer}/connect/endsession`,
    check_session_iframe: `${issuer}/connect/checksession`,
    revocation_endpoint: `${issuer}/connect/revocation`,
    introspection_endpoint: `${issuer}/connect/introspect`
};
