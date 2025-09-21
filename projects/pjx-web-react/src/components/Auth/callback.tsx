import React from "react";
import { AuthConsumer } from "../../providers/authProvider";
import Loading from '../Common/loading';

export const Callback = () => (
    <AuthConsumer>
        {({ signinRedirectCallback }) => {
            signinRedirectCallback();
            return <Loading />;
        }}
    </AuthConsumer>
);
