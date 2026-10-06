// CI/CD for Worktime on the self-hosted Jenkins (see the jenkins-local repo).
// Every new commit on main: tests → images tagged <build>-<sha> → release → rolling deploy → smoke test.
//
// A release is the compose file + deploy script of one tag, kept in $JENKINS_HOME/deploy-state/worktime/<tag>/.
// Deploys and rollbacks always run from the target tag's release, so configuration travels with the version:
// rolling back restores the old images AND the old compose config.
//
// A failed deploy rolls back to the last tag that went live. "Build with Parameters" + DEPLOY_TAG = manual rollback.
pipeline {
  agent any

  options {
    timestamps()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '30'))
    timeout(time: 30, unit: 'MINUTES')
  }

  parameters {
    string(name: 'DEPLOY_TAG', defaultValue: '',
      description: 'Vacío: probar, construir y desplegar este commit. Un tag existente (p. ej. 12-a1b2c3d): rollback a esa versión sin construir.')
  }

  environment {
    RELEASES = "${JENKINS_HOME}/deploy-state/worktime"
    LAST_GOOD = "${JENKINS_HOME}/deploy-state/worktime/last-good"
    KEEP_RELEASES = '5'
  }

  stages {
    stage('Versión') {
      steps {
        script {
          env.ROLLBACK_ONLY = params.DEPLOY_TAG?.trim() ? 'true' : 'false'
          env.TAG = env.ROLLBACK_ONLY == 'true' ? params.DEPLOY_TAG.trim() : "${env.BUILD_NUMBER}-${env.GIT_COMMIT.take(7)}"
          currentBuild.displayName = "#${env.BUILD_NUMBER} · ${env.TAG}${env.ROLLBACK_ONLY == 'true' ? ' (rollback)' : ''}"
        }
        sh '''
          mkdir -p "$RELEASES"
          echo "Versión en producción: $(cat "$LAST_GOOD" 2>/dev/null || echo ninguna)"
          echo "Releases disponibles para rollback: $(ls -1t "$RELEASES" | grep -v last-good | tr '\\n' ' ')"
          if [ "$ROLLBACK_ONLY" = true ] && [ ! -f "$RELEASES/$TAG/docker-compose.yml" ]; then
            echo "No existe el release $TAG. Usa uno de la lista de arriba." >&2; exit 1
          fi
        '''
      }
    }

    stage('Tests') {
      when { environment name: 'ROLLBACK_ONLY', value: 'false' }
      steps {
        // Throwaway SDK container sharing Jenkins' workspace volume. Integration tests start Postgres/Redis
        // through the host's Docker (Testcontainers) and reach them via host-gateway.
        sh '''
          docker run --rm --volumes-from jenkins -w "$WORKSPACE" \
            -v /var/run/docker.sock:/var/run/docker.sock \
            -v worktime-nuget:/root/.nuget/packages \
            --add-host host.docker.internal:host-gateway \
            -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal \
            mcr.microsoft.com/dotnet/sdk:8.0 \
            dotnet test Worktime.sln --logger "trx;LogFilePrefix=results" --results-directory "$WORKSPACE/test-results"
        '''
      }
      post {
        always { archiveArtifacts artifacts: 'test-results/*.trx', allowEmptyArchive: true }
      }
    }

    stage('Imágenes y release') {
      when { environment name: 'ROLLBACK_ONLY', value: 'false' }
      steps {
        // Refresh base images when the registry answers; if Docker Hub hiccups (502/rate limit), build from cache.
        sh 'WORKTIME_TAG="$TAG" docker compose build --pull || WORKTIME_TAG="$TAG" docker compose build'
        sh '''
          mkdir -p "$RELEASES/$TAG"
          cp docker-compose.yml deploy/rolling-deploy.sh "$RELEASES/$TAG/"
          git log -1 --format='%H %an: %s' > "$RELEASES/$TAG/commit.txt"
        '''
      }
    }

    stage('Deploy') {
      steps {
        script { env.DEPLOY_STARTED = 'true' }
        withCredentials([
          string(credentialsId: 'worktime-jwt-key', variable: 'JWT_SIGNING_KEY'),
          string(credentialsId: 'worktime-postgres-password', variable: 'POSTGRES_PASSWORD'),
        ]) {
          sh 'COMPOSE_FILE_PATH="$RELEASES/$TAG/docker-compose.yml" bash "$RELEASES/$TAG/rolling-deploy.sh" "$TAG"'
        }
      }
    }
  }

  post {
    success {
      sh '''
        echo "$TAG" > "$LAST_GOOD"
        # Keep the newest releases (and always the live one); their images are pruned by the deploy script.
        ls -1t "$RELEASES" | grep -v last-good | tail -n +"$((KEEP_RELEASES + 1))" | grep -vx "$TAG" \
          | xargs -r -I{} rm -rf "$RELEASES/{}"
      '''
      script { currentBuild.description = "En producción: ${env.TAG}" }
    }
    failure {
      script {
        // A version that never went live is not a rollback target.
        if (env.ROLLBACK_ONLY == 'false' && env.TAG) {
          sh 'rm -rf "$RELEASES/$TAG"'
        }
        if (env.DEPLOY_STARTED == 'true') {
          def previous = sh(script: 'cat "$LAST_GOOD" 2>/dev/null || true', returnStdout: true).trim()
          if (previous && previous != env.TAG) {
            echo "Deploy de ${env.TAG} falló: volviendo a ${previous} (imágenes y configuración de ese release)"
            withCredentials([
              string(credentialsId: 'worktime-jwt-key', variable: 'JWT_SIGNING_KEY'),
              string(credentialsId: 'worktime-postgres-password', variable: 'POSTGRES_PASSWORD'),
            ]) {
              withEnv(["PREVIOUS=${previous}"]) {
                sh 'COMPOSE_FILE_PATH="$RELEASES/$PREVIOUS/docker-compose.yml" bash "$RELEASES/$PREVIOUS/rolling-deploy.sh" "$PREVIOUS"'
              }
            }
            currentBuild.description = "Falló; se restauró ${previous}"
          } else {
            echo 'No hay una versión buena previa a la que volver.'
          }
        }
      }
    }
    always {
      sh 'docker image prune -f >/dev/null || true'
    }
  }
}
