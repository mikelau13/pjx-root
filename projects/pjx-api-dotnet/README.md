# pjx-api-dotnet

This repository is a group of .NET Core 3.1 dotnet core projects:

- [Pjx_Api](/src/Pjx_Api) - Calendar API gateway, authenticated by [pjx-sso-identityserver](https://github.com/mikelau13/pjx-sso-identityserver).
- [Pjx.CalendarLibrary](/src/Pjx.CalendarLibrary) - Business logic layer of the calendar API, with Repository pattern
- [Pjx.CalendarEntity](/src/Pjx.CalendarEntity) - Entity layer of the calendar libray, with Entity Framework 
- [Pjx.Calendar_Test](/src/Pjx.Calendar_Test) - TDD to test `Pjx.CalendarLibrary`, using [Autofac](https://autofac.org/) IoC container to mock the repositories
- [Pjx_CreateCertificates](/src/Pjx_CreateCertificates) - generate self-seigned certificates
- [Pjx_CreateCertificates_Test](/src/Pjx_CreateCertificates_Test) - TDD of the Pjx_CreateCertificates

This project is one of the components of the `pjx` application, please check [pjx-root](https://github.com/mikelau13/pjx-root) for more details.

## Pjx_Api

A simple API project developed with .Net Core 3.1, being consumed by [pjx-web-react](https://github.com/mikelau13/pjx-web-react).


### Dependencies

[pjx-sso-identityserver](https://github.com/mikelau13/pjx-sso-identityserver) - to handle the Api's authentications.

> Please follow the instructions in [pjx-sso-identityserver](https://github.com/mikelau13/pjx-sso-identityserver) to set up the `hosts` and trust the self-signed SSL certificate, otherwise this API might refuse to authenticate.


### Patterns

Repository Pattern, Unit of Work Pattern, Dependency Injection

> For complex scenarios, Microsoft recommanded using an abstraction layer of Repository Pattern over the EF Core, although the DbContext iteslf already based on Repository pattern - 
https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/infrastructure-persistence-layer-implementation-entity-framework-core#using-a-custom-repository-versus-using-ef-dbcontext-directly


### To run

Run this command:

```bash
docker-compose up
```

Or run it with dotnet for local debugging:

```bash
dotnet build && dotnet run
```

### To use with Swagger

Then you can visit the swagger UI: http://localhost:6001/swagger/ or the swagger specification: http://localhost:6001/swagger/v1/swagger.json

You will need the token to execute most of the APIs in this project, you can obtain a token from [pjx-sso-identityserver](https://github.com/mikelau13/pjx-sso-identityserver), please follow the steps to register in [pjx-root](https://github.com/mikelau13/pjx-root#using-the-web-app), login to the web page, then visit the `/country/all` page and get the key from the browser's developer tool.

![pjx get token](/images/api_swagger_key.png)

Then in the swagger page, click the `authorize` button, paste the token `Bearer xxxxxx`:

![pjx paste token](/images/api_swagger_authorize.png)

Then it should return successful response 200:

![pjx response 200](/images/api_swagger_response_200.png)


## To generate self-signed certificate for Identity Server

Execute the follow PowerShell script (but replace openssl path and output paths):

```
$certPass = "password"
$certSubj = "host.docker.internal"
$certAltNames = "DNS:localhost,DNS:pjx-sso-identityserver,DNS:host.docker.internal,DNS:127.0.0.1"
$opensslPath="C:\Program Files\Git\usr\bin"
$workDir="D:\Codes"
$dockerDir=Join-Path $workDir "ProjectApi"
Start-Process -NoNewWindow -Wait -FilePath (Join-Path $opensslPath "openssl.exe") -ArgumentList "req -x509 -nodes -days 365 -newkey rsa:2048 -keyout ",
                                          (Join-Path $workDir pjx-sso-identityserver.rsa_2048.cert.key),
                                          "-out", (Join-Path $dockerDir pjx-sso-identityserver.rsa_2048.cert.crt),
                                          "-subj `"/CN=$certSubj`" -addext `"subjectAltName=$certAltNames`""
										  
Start-Process -NoNewWindow -Wait -FilePath (Join-Path $opensslPath "openssl.exe") -ArgumentList "pkcs12 -export -in ", 
                                           (Join-Path $dockerDir pjx-sso-identityserver.rsa_2048.cert.crt),
                                           "-inkey ", (Join-Path $workDir pjx-sso-identityserver.rsa_2048.cert.key),
                                           "-out ", (Join-Path $workDir pjx-sso-identityserver.rsa_2048.cert.pfx),
                                           "-passout pass:$certPass"							   
										   
#this will prompt for the password
$cert = Get-PfxCertificate -FilePath (Join-Path $workDir "pjx-sso-identityserver.rsa_2048.cert.pfx") 
```


## Docker Hub registry

To push docker image:
```
docker login
docker image ls
docker tag xxxxxxxxxxxxxxxxx mikelauawaremd/pjx-api-dotnet:v0.0.yy
docker push mikelauawaremd/pjx-api-dotnet:v0.0.yy
```

[Pull image from Docker Hub registry at mikelauawaremd/pjx-api-dotnet/v0.0.1](https://hub.docker.com/layers/mikelauawaremd/pjx-api-dotnet/v0.0.1/images/sha256-391b16125cdbdff9d8e924dd678d8ca6fd3b87bf13f027a35db5beb9bb53455c?tab=layers)
```
docker pull mikelauawaremd/pjx-api-dotnet:v0.0.1
```

[Apply Kubernetes deployment at pjx/kubernetes/pjx-api-dotnet.yaml](https://github.com/mikelau13/pjx-root/tree/master/kubernetes/pjx-api-dotnet.yaml)
```
kubectl apply -f pjx-web-react.yaml --namespace=pjx
```
