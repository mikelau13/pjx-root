#

## System Requirements

[Install Helm Charts](https://helm.sh/docs/intro/install/)

```
choco install kubernetes-helm
```

Note: to install [Chocolatey](https://chocolatey.org/install)

Note: learn CRD (Customer Resource Definition)


### Steps

Use this [SQL Server 2022 Linux Helm Chart](https://artifacthub.io/packages/helm/simcube/mssqlserver-2022).

Installed successfully
```cmd
helm repo add simcube https://simcubeltd.github.io/simcube-helm-charts/
helm search hub simcube
helm install mymssql simcube/mssqlserver-2022 --version 1.2.3 --set acceptEula.value=Y --set sapassword=Testing1122 --set edition.value=Developer --namespace pjx
kubectl get all
```

To double check the password:
```cmd
printf $(kubectl get secret --namespace default mymssql-mssqlserver-2022-secret -o jsonpath="{.data.sapassword}" | base64 --decode);echo
```

Spin up a pod which has sqlcmd installed and enter a bash session:
```cmd
kubectl run mssqlcli --image=mcr.microsoft.com/mssql-tools -ti --restart=Never --rm=true -- /bin/bash
sqlcmd -S mymssql-mssqlserver-2022 -U sa
sqlcmd -S mssqlcli -U sa
```


To extract
```cmd
helm pull simcube/mssqlserver-2022
tar -xvzf mssqlserver-2022-1.2.3.tg
```

