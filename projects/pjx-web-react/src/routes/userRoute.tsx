import React from 'react';
import { Route } from 'react-router-dom';
import { AuthConsumer } from '../providers/authProvider';
import Loading from '../components/Common/loading';

export const UserRoute = ({ component, ...rest }: any) => {
    const renderFn = (Component: any) => (props: any) => (
        <AuthConsumer>
            {({ getUser, signinRedirect }) => {
                var user = getUser();
                if (!!Component && user) {
                    return (
                        <div>
                            <Component {...props} />
                        </div>
                    );
                } else {
                    signinRedirect();
                    return <Loading />;
                }
            }}
        </AuthConsumer>
    );

    return <Route {...rest} render={renderFn(component)} />;
};
