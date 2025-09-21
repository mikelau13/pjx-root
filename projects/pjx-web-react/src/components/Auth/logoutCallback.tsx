import React from "react";
import { AuthConsumer } from "../../providers/authProvider";
import Loading from '../Common/loading';

export const LogoutCallback = () => (
    <AuthConsumer>
        {({ signoutRedirectCallback }) => {
            signoutRedirectCallback();
            return <Loading />;
        }}
    </AuthConsumer>
);
