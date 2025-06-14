# Настройка CI/CD Pipeline

Для работы CI/CD pipeline необходимо настроить следующие секреты в настройках репозитория GitHub.

## Требуемые секреты

### Docker Hub
- `DOCKER_HUB_USERNAME`: Имя пользователя в Docker Hub
- `DOCKER_HUB_TOKEN`: Токен доступа к Docker Hub (не используйте пароль)

### SonarQube
- `SONAR_TOKEN`: Токен доступа к SonarQube
- `SONAR_HOST_URL`: URL вашего сервера SonarQube (например, `https://sonarqube.yourcompany.com`)

## Инструкция по настройке

1. Перейдите в репозиторий на GitHub
2. Нажмите на вкладку **Settings**
3. В левом меню выберите **Secrets and variables → Actions**
4. Нажмите **New repository secret**
5. Добавьте каждый из вышеперечисленных секретов

## Получение токенов

### Docker Hub
1. Войдите в свою учетную запись Docker Hub
2. Перейдите в **Account Settings → Security**
3. Нажмите **New Access Token**
4. Укажите имя и выберите необходимые разрешения
5. Скопируйте созданный токен (он будет показан только один раз)

### SonarQube
1. Войдите в SonarQube с правами администратора
2. Перейдите в **My Accaunt → Security**
3. Cоздайте новый токен
4. Скопируйте созданный токен (он будет показан только один раз) 