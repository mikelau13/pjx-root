import React from 'react';
import { Route } from 'react-router-dom';
import { AuthConsumer } from '../providers/authProvider';
import Loading from '../components/Common/loading';

export const PrivateRoute = ({ component, ...rest }: any) => {
    const renderFn = (Component: any) => (props: any) => (
        <AuthConsumer>
            {({ isAuthenticated, signinRedirect }) => {
                if (!!Component && isAuthenticated()) {
                    return <Component {...props} />;
                } else {
                    signinRedirect();
                    return <Loading />;
                }
            }}
        </AuthConsumer>
    );

    return <Route {...rest} render={renderFn(component)} />;
};
