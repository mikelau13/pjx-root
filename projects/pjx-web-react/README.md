# pjx-web-react

## Dependencies

This projet is a web client that depends other projects, such that identity server, Apollo server, APIs.  

In order to launch the entire solution, please refer to instructions in [pjx-root](https://github.com/mikelau13/pjx-root#running-a-solution).


## Libraries 

- [oidc-client](https://github.com/IdentityModel/oidc-client-js/wiki) - to connect to the [identity server](https://github.com/mikelau13/pjx-sso-identityserver).
- [apollo-client](https://www.apollographql.com/docs/react/) - to hook to the [pjx-graphql-apollo](https://github.com/mikelau13/pjx-graphql-apollo).
- [Material-UI](https://material-ui.com/) - a React UI framework
- [FullCalendar](https://fullcalendar.io/) - a JavaScript Calendar


## Dependencies

This web client is depending on other components such that APIs, Identity Server, Apollo Server, etc; so please follow the instructions in [pjx-root](https://github.com/mikelau13/pjx-root) if you want to run the complete solution.


## Running the web client

To launch the app in Docker container, run:

```bash
docker-compose up 
```

Alternatively, in the project directory, you can run for development purpose:

```bash
npm start
```

Runs the app in the development mode.<br />

Open [http://localhost:3000](http://localhost:3000) to view it in the browser.

![pjx web react](/images/mobile_desktop.png)

Follow the instructions on [pjx-root](https://github.com/mikelau13/pjx-root#using-the-web-app) for how to browse this demo web site.


```bash
npm run build
```

See the section about [deployment](https://facebook.github.io/create-react-app/docs/deployment) for more information.

Open [http://localhost:3000](http://localhost:3000) to view it in the browser.  Please also follow the instructions on [pjx-sso-identityserver](https://github.com/mikelau13/pjx-sso-identityserver) to set up `hosts` file and trust the self-signed SSL certificate.

By clicking on the `dashboard` button, the page will forward you to the `pjx-sso-identityserver` to sign on.



## Running bash commands from the container

Please run all bash(terminal) related commands from the `pjx-web-react-dev` container. This includes:

- Installing new npm modules
- Generating types
- Pretty much all the scripts commands in the package.json file

The one exception to this rule is the below command that will open an interactive terminal for the `pjx-web-react-dev` container. Once in this interactive terminal you can now run your commands the same as you would from your local terminal.

```bash
# run from your computer's terminal
npm run docker:exec
```

If successful you should see the following in your termianl:

```bash
# This is showing me logged in as root user.
root@2lsk39bd:/usr/app
```

To exit the interactive terminal run the following:

```bash
exit
```

## TODO 

- configure challenge code for `oidc-client` with `identity server`

