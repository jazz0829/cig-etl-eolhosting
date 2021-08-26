pipeline {
	agent {
		node {
			label 'mario-windows-dotnet'
			customWorkspace '/ws'
		}
	}
	stages {
		stage ('Build, test') {
			steps {
				bat "powershell.exe -File build.ps1 -Target Test"
			}
		}
		stage('SonarQube') {
			steps {
				bat "powershell.exe -File build.ps1 -Target Sonar"
			}
		}
	}
	post {
  		always {
			archive '/build/output/**, DeploymentScripts/**'
 			deleteDir()
  		}
  	}
}