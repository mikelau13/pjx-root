import React from "react";
import { AuthConsumer } from "../../providers/authProvider";
import Loading from '../Common/loading';

export const SilentRenew = () => (
    <AuthConsumer>
        {({ signinSilentCallback }) => {
            signinSilentCallback();
            return <Loading />;
        }}
    </AuthConsumer>
);
