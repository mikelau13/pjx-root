import React from 'react';
import { ApolloProvider } from '@apollo/react-hooks';
import createApolloClient from './apolloClient';

// On the client, we store the Apollo Client in the following variable.
// This prevents the client from reinitializing between page transitions.
let globalApolloClient:any = null;

/**
 * Installs the Apollo Client on NextPageContext
 * or NextAppContext. Useful if you want to use apolloClient
 * inside getStaticProps, getStaticPaths or getServerSideProps
 * @param {NextPageContext | NextAppContext} ctx
 */
export const initOnContext = (ctx:any) => {
    const inAppContext = Boolean(ctx.ctx);

    // We consider installing `withApollo({ ssr: true })` on global App level
    // as antipattern since it disables project wide Automatic Static Optimization.
    if (process.env.NODE_ENV === 'development') {
        if (inAppContext) {
            console.warn(
                'Warning: You have opted-out of Automatic Static Optimization due to `withApollo` in `pages/_app`.\n' +
                    'Read more: https://err.sh/next.js/opt-out-auto-static-optimization\n'
            );
        }
    }

    // Initialize ApolloClient if not already done
    const apolloClient =
        ctx.apolloClient ||
        initApolloClient(ctx.apolloState || {}, inAppContext ? ctx.ctx : ctx);

    // We send the Apollo Client as a prop to the component to avoid calling initApollo() twice in the server.
    // Otherwise, the component would have to call initApollo() again but this
    // time without the context. Once that happens, the following code will make sure we send
    // the prop as `null` to the browser.
    apolloClient.toJSON = () => null;

    // Add apolloClient to NextPageContext & NextAppContext.
    // This allows us to consume the apolloClient inside our
    // custom `getInitialProps({ apolloClient })`.
    ctx.apolloClient = apolloClient;
    if (inAppContext) {
        ctx.ctx.apolloClient = apolloClient;
    }

    return ctx;
};

/**
 * Always creates a new apollo client on the server
 * Creates or reuses apollo client in the browser.
 * @param  {NormalizedCacheObject} initialState
 * @param  {NextPageContext} ctx
 */
const initApolloClient = (initialState:any, ctx:any) => {
    // Make sure to create a new client for every server-side request so that data
    // isn't shared between connections (which would be bad)
    if (typeof window === 'undefined') {
        return createApolloClient(initialState, ctx);
    }

    // Reuse client on the client-side
    if (!globalApolloClient) {
        globalApolloClient = createApolloClient(initialState, ctx);
    }

    return globalApolloClient;
};

/**
 * Creates a withApollo HOC
 * @param  {Object} withApolloOptions
 * @param  {Boolean} [withApolloOptions.ssr=false]
 * @returns {(PageComponent: ReactNode) => ReactNode}
 */
export const withApollo = ({ ssr = false } = {}) => (PageComponent:any) => {
    const WithApollo = ({ apolloClient, apolloState, ...pageProps }:any) => {
        let client;
        if (apolloClient) {
            client = apolloClient;
        } else {
            client = initApolloClient(apolloState, undefined);
        }

        return (
            <ApolloProvider client={client}>
                <PageComponent {...pageProps} />
            </ApolloProvider>
        );
    };

    // Set the correct displayName in development
    if (process.env.NODE_ENV !== 'production') {
        const displayName =
            PageComponent.displayName || PageComponent.name || 'Component';
        WithApollo.displayName = `withApollo(${displayName})`;
    }

    if (ssr || PageComponent.getInitialProps) {
        WithApollo.getInitialProps = async (ctx:any) => {
            const inAppContext = Boolean(ctx.ctx);
            const { apolloClient } = initOnContext(ctx);

            // Run wrapped getInitialProps methods
            let pageProps = {};
            if (PageComponent.getInitialProps) {
                pageProps = await PageComponent.getInitialProps(ctx);
            } else if (inAppContext) {
                //pageProps = await App.getInitialProps(ctx);
            }

            return {
                ...pageProps,
                // Extract query data from the Apollo store
                apolloState: apolloClient.cache.extract(),
                // Provide the client for ssr. As soon as this payload
                // gets JSON.stringified it will remove itself.
                apolloClient: ctx.apolloClient,
            };
        };
    }

    return WithApollo;
};
