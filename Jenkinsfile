pipeline {
  agent any
  stages {
    stage('Cleanup Workspace') {
      steps {
        cleanWs()
      }
    }

    stage('Checkout') {
      steps {
        dir(path: 'pjx-graphql-apollo') {
          sh 'pwd'
          git(url: 'https://github.com/mikelau13/pjx-graphql-apollo.git', branch: 'master')
        }

        sh 'pwd'
      }
    }

  }
}