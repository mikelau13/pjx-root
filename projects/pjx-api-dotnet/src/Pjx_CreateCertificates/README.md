
To generate self-signed certification, execute:
```bash
dotnet run `dnsName` `password` `RSA|ECDSA"
```

For example: 

```bash
dotnet run "localhost" "password" "RSA"
dotnet run "pjx-sso-identity" "password" "RSA"
```