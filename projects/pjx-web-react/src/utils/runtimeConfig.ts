// src/utils/runtimeConfig.ts
if (!(window as any).__PJX_CONFIG__) {
  console.warn('[pjx] config.js did not load — falling back to build-time values');
}
const rc = (window as any).__PJX_CONFIG__ ?? {};

export const config = {
  graphqlEndpoint: rc.GRAPHQL_ENDPOINT  ?? process.env.REACT_APP_GRAPHQL_ENDPOINT,
  ssoIssuerUrl:    rc.SSO_ISSUER_URL    ?? process.env.REACT_APP_SSO_ISSUER_URL,
  ssoClientId:     rc.SSO_CLIENT_ID     ?? process.env.REACT_APP_SSO_CLIENT_ID,
  apiDotnetUrl:    rc.API_DOTNET_URL    ?? process.env.REACT_APP_API_DOTNET_URL,
  publicUrl:       rc.PUBLIC_URL        ?? process.env.REACT_APP_PUBLIC_URL,
};
