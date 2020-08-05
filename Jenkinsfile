pipeline {
  agent any
  stages {
    stage('Checkout') {
      steps {
        dir(path: 'projects/pjx-graphql-apollo') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-graphql-apollo.git'
        }

        dir(path: 'projects/pjx-api-node') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-api-node.git'
          sh 'ls -l'
        }

        dir(path: 'projects/pjx-sso-identityserver') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-sso-identityserver.git'
        }

        dir(path: 'projects/pjx-api-dotnet') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-api-dotnet.git'
        }

        dir(path: 'projects/pjx-web-react') {
          sh 'pwd'
          git 'https://github.com/mikelau13/pjx-web-react.git'
        }

        sh 'pwd'
        sh 'ls -l'
      }
    }

    stage('Build') {
      steps {
        sh 'docker-compose build'
      }
    }

    stage('Launch') {
      steps {
        sh 'docker-compose up --no-build -d'
        sleep 10
      }
    }

    stage('Test') {
      steps {
        sh 'docker ps'
      }
    }

    stage('Proceed clean up? ') {
      steps {
        input(message: 'Proceed to Cleanup?', ok: 'Yes')
      }
    }

    stage('Clean up') {
      steps {
        sh 'docker-compose down'
        sh 'docker system prune'
        cleanWs()
      }
    }

  }
}