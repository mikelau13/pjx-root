import React from "react";
import { AuthConsumer } from "../../providers/authProvider";
import Loading from '../Common/loading';

export const Logout = () => (
    <AuthConsumer>
        {({ logout }) => {
            logout();
            return <Loading />;
        }}
    </AuthConsumer>
);
