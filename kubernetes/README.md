# pjx-root/kubernetes

## Overview

Kubernetes provisioning solution for <a href='https://github.com/users/mikelau13/projects/1'>pjx</a> application.


## Installation

### System Requirements

Please see instructions on how to setup the <a href='https://github.com/mikelau13/pjx-root/blob/master/README.md'>pjx docker environments</a>.

<a href='https://minikube.sigs.k8s.io/'>Minikube</a>


### Launch the application

Install Minikube (strongly recommanded using Hyper-V driver) and enable Ingress
```ps
minikube start --driver=hyperv
minikube addons enable ingress
```

Then apply the Kubernetes Namespace, Config and Secret:
```ps
kubectl apply -f pjx-namespace.yaml
kubectl apply -f pjx-config.yaml --namespace=pjx
kubectl apply -f pjx-secret.yaml --namespace=pjx
```

Then apply the Kubernetes services in the pjx projects:
```ps
kubectl apply -f pjx-api-dotnet.yaml --namespace=pjx
kubectl apply -f pjx-api-node.yaml --namespace=pjx
kubectl apply -f pjx-graphql-apollo.yaml --namespace=pjx
kubectl apply -f pjx-web-react.yaml --namespace=pjx
kubectl apply -f pjx-sso-identityserver.yaml --namespace=pjx
```

Apply the Kubernetes Ingress:
```ps
kubectl apply -f pjx-ingress.yaml --namespace=pjx
kubectl get ingress -n pjx
```

### Test the application

Test these links in web browser:
- http://api.pjx.com:30601/api/calendar/healthcheck
- http://api.pjx.com/api/calendar/healthcheck
- https://api.pjx.com/api/calendar/healthcheck
- https://www.pjx.com/
- http://ql.pjx.com:30400/


To open Minikube Kubernetes Dashboard:
```ps
minikube dashboard
```

Or these commands to help to check the status of the minikube:
```ps
kubectl get pod -n pjx
kubectl get all -n pjx
minikube service list
minikube ip
```
