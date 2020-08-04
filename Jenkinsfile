pipeline {
  agent any
  stages {
    stage('Checkout') {      
      steps {
        dir('pjx-graphql-apollo') {
          sh 'pwd'
          git(url: 'https://github.com/mikelau13/pjx-graphql-apollo.git', branch: 'master')
        }
        sh 'pwd'
      }
    }

  }
}
