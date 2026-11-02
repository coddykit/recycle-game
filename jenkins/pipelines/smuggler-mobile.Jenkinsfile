// Games/smuggler-mobile: Linux Godot BUILD-ONLY job (from games/jenkins/game-android-build-only.Jenkinsfile.in, Patron 2026-10-27).
// No timers, no upload stage, no Play credentials: archives AAB + symbols + version_code.txt + release.json; the Patron
// uploads separately (store-automation gp-upload-aab.ts). Game-specific like pit-wall: gen_pipelines.sh must not overwrite
// it with the plain template (keeps games/tools in the sparse checkout, the agent alias, release.json, Gradle cleanup).
pipeline {
    agent { label 'android-linux || linux-android' }
    options {
        disableConcurrentBuilds()
        timeout(time: 180, unit: 'MINUTES')
        buildDiscarder(logRotator(numToKeepStr: '10'))
        skipDefaultCheckout()
    }
    parameters {
        choice(name: 'PLATFORM', choices: ['android'], description: 'Android only')
        choice(name: 'ANDROID_ACTION', choices: ['build-only'], description: 'Signed AAB only; never uploads')
    }
    environment {
        REPO_GIT_URL = 'git@github.com:coddykit/CoddyApps.git'
        GAME = 'smuggler'
        GAME_SERVICES_MODE = 'production'
        JAVA_HOME = '/usr/lib/jvm/java-17-openjdk-amd64'
        ANDROID_HOME = '/opt/android-sdk'
        ANDROID_SDK_ROOT = '/opt/android-sdk'
        TMPDIR = "${HOME}/tmp"
        GIT_LFS_SKIP_SMUDGE = '1'
    }
    stages {
        stage('Checkout') {
            steps {
                sh '''
                    set -eu
                    mkdir -p "$TMPDIR"
                    if [ ! -d repo/.git ]; then
                        git clone --filter=blob:none --no-checkout "$REPO_GIT_URL" repo
                        git -C repo sparse-checkout init --cone
                    fi
                    git -C repo sparse-checkout set "games/$GAME" games/jenkins games/tools
                    git -C repo fetch origin main
                    git -C repo checkout -f -B main origin/main
                    git -C repo clean -fd -- "games/$GAME" games/jenkins games/tools
                    git -C repo log -1 --format='commit %h %ci %s'
                '''
            }
        }
        stage('Production configuration') {
            steps {
                // The Linux agent must have Godot 4.7.2/templates/signing and real game configuration.
                sh '''
                    if [ -f "repo/games/$GAME/jenkins/preflight.sh" ]; then
                        sh "repo/games/$GAME/jenkins/preflight.sh"
                    else
                        python3 "repo/games/$GAME/tools/services_preflight.py" --production
                    fi
                '''
            }
        }
        stage('Tests') {
            steps { sh 'sh repo/games/jenkins/run.sh "$GAME" test' }
        }
        stage('Android build-only') {
            steps { sh 'sh repo/games/jenkins/run.sh "$GAME" android build-only' }
        }
        stage('Archive') {
            steps {
                archiveArtifacts artifacts: 'repo/games/smuggler/build/android/borderlineempire.aab,repo/games/smuggler/build/android/borderlineempire.symbols.zip,repo/games/smuggler/build/android/version_code.txt,repo/games/smuggler/build/android/release.json', fingerprint: true
            }
        }
    }
    post {
        always {
            archiveArtifacts artifacts: 'repo/games/smuggler/build/android/export.log,repo/games/smuggler/build/jenkins/*.log', allowEmptyArchive: true
            // Agent disk is small (79 GB, ~3 GB free on 2026-10-27): drop this job's Gradle intermediates (~1.3 GB,
            // regenerated next build); the template (android/build), .godot cache and the archived AAB stay.
            sh 'G="$WORKSPACE/repo/games/$GAME/android/build/build"; [ ! -d "$G" ] || rm -rf "${G:?}"'
        }
    }
}
