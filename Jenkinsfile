pipeline {
  agent any
  stages {
    stage('Checkout') {
      steps {
        dir(path: 'pjx-graphql-apollo') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-graphql-apollo.git'
        }

        dir(path: 'pjx-api-node') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-api-node.git'
          sh 'ls -l'
        }

        dir(path: 'pjx-sso-identityserver') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-sso-identityserver.git'
        }

        dir(path: 'pjx-api-dotnet') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-api-dotnet.git'
        }

        dir(path: 'pjx-web-react') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-web-react.git'
        }

        sh 'pwd'
        sh 'ls -l'
      }
    }

    stage('Cleanup Workspace') {
      steps {
        cleanWs()
      }
    }

  }
}