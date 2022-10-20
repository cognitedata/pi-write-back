@Library('jenkins-helpers')

nugetPath = "nuget.exe"
msbuild = 'C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\BuildTools\\MSBuild\\Current\\Bin\\msbuild.exe'

node ('windows')
{
    try
    {
        stage ('Checkout')
        {
            checkout([$class: 'GitSCM',
                branches: scm.branches,
                extensions: [
                    [ $class: 'SubmoduleOption',
                      disableSubmodules: false,
                      parentCredentials: true,
                      recursiveSubmodules: true,
                      reference: '',
                      trackingSubmodules: false],
                    [ $class: 'CleanCheckout' ]
                ],
                userRemoteConfigs: scm.userRemoteConfigs
            ])
            try {
                version = powershell(returnStdout: true, script: "git describe --tags HEAD").trim()
                version = version.replaceFirst(/-(\d+)-.*/, '-pre.$1')
                lastTag = powershell(returnStdout: true, script: "git describe --tags --abbrev=0").trim()
                desc = powershell(returnStdout: true, script: "git describe --tags --dirty").trim()
                time = powershell(returnStdout: true, script: "git log -1  --format=%ai").trim()
                branch = "${env.BRANCH_NAME}".trim()
                echo "\"$version\""
                echo "$desc"
                echo "\"$lastTag\""
                echo "[${branch}]"
                
                buildInstaller = ("$lastTag" == "$version" && "${branch}" == "master")
            }
            catch (e) {
                buildInstaller = false
            }
            if ( buildInstaller ) {
                echo "Will build installer"
            }
            else {
                echo "Won't build installer"
            }
        }

        stage ('Build solution') {
            powershell("dotnet build")
        }
        
        timeout(10) {
            stage ('Run tests') {
                withCredentials([
                    string(credentialsId: 'piserverpassword', variable: 'piPassword')
                ]) {
                    powershell(".\\run-tests.ps1 34.77.200.87 cognitepi ${piPassword}")
                }
            }
        }
        
        if ( buildInstaller ) {
            stage ('Build MSI') {
                powershell(".\\publish.ps1 \"${lastTag}\" \"${desc} ${time}\"")

                codeSign.signOnWindows('buildoutput\\PiWriteBack.exe')

                dir ('.\\PiWriteBack.Setup\\') {
                    powershell(".\\build.ps1 -v \"${lastTag}\" -b \"${msbuild}\" -d \"${desc} ${time}\" -c \"setup-config.json\"")
                }
                codeSign.signOnWindows('PiWriteBack.Setup\\bin\\Release\\PiWriteBackSetup.msi')
            }
            stage ('Deploy to github') {
                powershell("mv PiWriteBack.Setup\\bin\\Release\\PiWriteBackSetup.msi .\\PiWriteBackSetup-${lastTag}.msi")
                withCredentials([usernamePassword(credentialsId: 'githubapp', usernameVariable: 'ghusername', passwordVariable: 'ghpassword')]) {
                    powershell("py deploy.py cognitedata pi-write-back $ghpassword $lastTag PiWriteBackSetup-${lastTag}.msi")
                }
            }
        }
    }
    catch (e)
    {
        currentBuild.result = "FAILURE"
        echo "Exception message: ${e.getMessage()}"
        throw e
    }
    finally
    {
        stage('Cleanup') {
            deleteDir()
        }
    }
}
