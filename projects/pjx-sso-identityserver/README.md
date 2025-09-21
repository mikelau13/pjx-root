# pjx-sso-identityserver

Single Sign On using [IdentityServer4](https://identityserver4.readthedocs.io/)

This project is one of the components of the `pjx` application, please check [pjx-root](https://github.com/mikelau13/pjx-root) for more details

## Prerequisites

The project is built with .NET Core SDK 3.1, here is how to [check if .NET Core is already installed](https://docs.microsoft.com/en-us/dotnet/core/install/how-to-detect-installed-versions?pivots=os-windows) or not.

Open Project with file `IdentityServer.csproj` inside [Visual Studio 2019](https://visualstudio.microsoft.com/vs/older-downloads/)

See how to install [.NET Core SDK and Runtime](https://docs.microsoft.com/en-us/dotnet/core/install/linux-ubuntu) 3.1 in Ubuntu.

Use [DB Browser for SQLite](https://sqlitebrowser.org/) to open the database file `AspIdUsers.db`


## To Open the Project

Use Visual Studio 2019, open Project with file `IdentityServer.csproj`.

Or open folder in Visual Studio Code, install [extensions from marketplace](https://code.visualstudio.com/docs/languages/csharp).

## Hosts and Self-signed SSL

First of all, need to configure the `hosts` file to map `127.0.0.1` with `pjx-sso-identityserver`:
```
127.0.0.1   pjx-sso-identityserver
```

Usually you can edit the `hosts` file with `sudo nano /etc/hosts` on Linux, or the file is located at `C:\Windows\System32\Drivers\etc\hosts` for Windows.

To start the docker container (app will run at https://pjx-sso-identityserver):

```bash
docker-compose up
```

Or, run this command line in terminal  (app will run at http://localhost:5001):

```bash
dotnet run
```

Or open the project in Visual Studio 2019, click `Start Debugging`.

Since the app is currently using a `self-signed SSL certificate`, most web browsers will consider this site "unsafe" and would block you from accessing the site. To continuous testing this app on local computer, you will need to configure your web browsers to allow the access.

For example, this is how to do this in [Chrome](https://support.google.com/chrome/answer/99020?co=GENIE.Platform%3DDesktop&hl=en).

Then you can navigate to the discovery document - https://pjx-sso-identityserver:5002/.well-known/openid-configuration

The default OpenID Connect protocol MVC UI have been set up, you can navigate to the login page - https://pjx-sso-identityserver:5002/Account/Login

To register a new user, you will need to visit the `/register` page in the [pjx-web-react](https://github.com/mikelau13/pjx-web-react) web client.


## Swagger

Path `/swagger`


# TODO: 

Instead of localhost and hosts file, need a real domain - then set up Google+ API by visiting [Google Console](https://console.developers.google.com/), create a new project, enable the `Google+ API`, then `Create credentials`, `configure OAuth consent screen`, and configure the callback address, finally uncomment codes in Startup.cs

Store the client and secret into key vault.
