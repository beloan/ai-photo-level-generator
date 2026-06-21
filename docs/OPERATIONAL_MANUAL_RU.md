# ОПЕРАЦИОННЫЙ MANUAL
## ИИ Система генерации уровней из фотографий

---

## СОДЕРЖАНИЕ
1. [Системные требования](#системные-требования)
2. [Установка и запуск](#установка-и-запуск)
3. [Основные операции](#основные-операции)
4. [Решение проблем](#решение-проблем)
5. [Техническая поддержка](#техническая-поддержка)

---

## СИСТЕМНЫЕ ТРЕБОВАНИЯ

### Минимальные требования:
- **ОС:** Windows 10/11, macOS 10.15+, Linux (Ubuntu 20.04+)
- **Процессор:** Intel i5 или AMD Ryzen 5
- **ОЗУ:** 4 GB (8 GB рекомендуется)
- **Место на диске:** 500 MB
- **.NET:** 8.0 SDK установлен

### Оптимальные требования:
- **ОС:** Windows 11 / Ubuntu 22.04
- **Процессор:** Intel i7/i9 или AMD Ryzen 7+
- **ОЗУ:** 16 GB
- **Место на диске:** 2 GB (для кэша)
- **GPU:** NVIDIA CUDA (для будущих версий)

### Проверка требований:
```bash
# Проверка .NET
dotnet --version

# Должна быть версия 8.0 или выше
```

---

## УСТАНОВКА И ЗАПУСК

### ШАГ 1: Подготовка
```bash
# 1. Откройте Command Prompt/PowerShell
# 2. Перейдите в папку проекта
cd c:\Диплом

# 3. Установите зависимости
dotnet restore
```

### ШАГ 2: Первый запуск (Консольное приложение)
```bash
# Проверка всего работает
dotnet build
dotnet run ConsoleLevelGenerator.cs

# Ожидаемый вывод:
# ✓ Test image created and processed. Check 'output.png'
# ✓ Generated 245 platforms
# ✓ Level data saved to 'level.json'
# ✓ Ready for Unity integration!
```

### ШАГ 3: REST API сервер
```bash
# Запуск веб-сервиса
dotnet run

# Сервер будет доступен по адресу:
# https://localhost:5001

# Для остановки: нажмите Ctrl+C
```

### ШАГ 4: Docker (опционально)
```bash
# Если Docker установлен:
docker-compose up -d

# Проверка статуса:
docker-compose ps
```

---

## ОСНОВНЫЕ ОПЕРАЦИИ

### ОПЕРАЦИЯ 1: Генерация уровня из изображения

#### Вариант A: Через консоль
```bash
# 1. Поместите изображение в папку c:\Диплом\
# 2. Отредактируйте ConsoleLevelGenerator.cs (линия 13)
# 3. Измените "input.png" на имя вашего файла
# 4. Запустите:
dotnet run ConsoleLevelGenerator.cs
```

#### Вариант B: Через программу
```csharp
// Создайте файл test.cs
var analyzer = new AdvancedImageAnalyzer();
var levelData = analyzer.AnalyzeWithAdvancedVision("my_image.png");

var generator = new LevelGenerator();
generator.SaveLevelToJson(levelData, "result.json");

Console.WriteLine("Готово!");
```

#### Вариант C: Через API
```bash
# Запустите сервер:
dotnet run

# На другой консоли выполните:
curl -F "imageFile=@image.png" ^
  https://localhost:5001/api/levelgeneration/generate
```

### ОПЕРАЦИЯ 2: Загрузка уровня в Unity

1. **Скопируйте файлы:**
   - `LevelLoaderUnity.cs` → `Assets/Scripts/`
   - `PlatformBuilder.cs` → `Assets/Scripts/`
   - `LevelManager.cs` → `Assets/Scripts/`
   - `level.json` → `Assets/Resources/`

2. **Создайте GameObject:**
   - Иерархия → Create Empty
   - Назовите "LevelManager"
   - Добавьте компонент "LevelManager"

3. **Создайте контейнер платформ:**
   - Create Empty Child (назовите "Platforms")

4. **Создайте Prefab платформы:**
   - 2D Sprite → Square
   - Назовите "PlatformPrefab"
   - Добавьте Box Collider 2D
   - Добавьте Rigidbody 2D (Body Type: Static)

5. **Настройте параметры:**
   - Platform Prefab: выберите PlatformPrefab
   - Platform Container: выберите Platforms
   - Auto Load On Start: включите

6. **Запустите сцену:**
   - Press Play в Unity
   - Уровень автоматически загружается

### ОПЕРАЦИЯ 3: Обработка нескольких изображений

```bash
# Создайте папку с изображениями
mkdir images

# Создайте batch-скрипт (run-batch.bat):
@echo off
for %%f in (images\*.png) do (
    echo Processing %%f
    dotnet run -- "%%f"
)
echo Done!

# Запустите:
run-batch.bat
```

### ОПЕРАЦИЯ 4: Резервное копирование результатов

```bash
# Создайте резервную копию
mkdir backup
copy output.png backup\output.%date:~10,4%%date:~4,2%%date:~7,2%.png
copy level.json backup\level.%date:~10,4%%date:~4,2%%date:~7,2%.json
```

---

## РЕШЕНИЕ ПРОБЛЕМ

### ПРОБЛЕМА 1: "Файл не найден"
```
Ошибка: System.IO.FileNotFoundException

Решение:
1. Проверьте, что файл находится в папке c:\Диплом\
2. Проверьте правильность имени файла (учитывается регистр на Linux)
3. Используйте полный путь: C:\Users\YourName\Documents\image.png
```

### ПРОБЛЕМА 2: "Порт 5001 уже используется"
```
Ошибка: System.IO.IOException: Failed to bind to address

Решение (Windows):
```bash
# Найдите процесс
netstat -ano | findstr :5001

# Завершите процесс (замените 1234 на PID)
taskkill /PID 1234 /F
```

Решение (Linux/Mac):
```bash
lsof -i :5001
kill -9 <PID>
```
```

### ПРОБЛЕМА 3: ".NET SDK не установлен"
```
Ошибка: 'dotnet' is not recognized

Решение:
1. Скачайте .NET 8.0 SDK с https://dotnet.microsoft.com/
2. Установите с default параметрами
3. Перезагрузите компьютер
4. Проверьте: dotnet --version
```

### ПРОБЛЕМА 4: HTTPS сертификат ошибка
```
Ошибка: SSL: TLSV1_ALERT_UNKNOWN_CA

Решение (для локального тестирования):
```bash
# Отключите проверку сертификата
$env:NODE_TLS_REJECT_UNAUTHORIZED=0

# Или используйте curl с флагом:
curl --insecure -F "imageFile=@image.png" https://localhost:5001/...
```
```

### ПРОБЛЕМА 5: Недостаточно памяти
```
Ошибка: OutOfMemoryException

Решение:
1. Закройте другие приложения
2. Используйте меньшее разрешение изображения
3. Обрабатывайте большие изображения по частям
```

### ПРОБЛЕМА 6: Нет прав на запись
```
Ошибка: UnauthorizedAccessException

Решение:
1. Убедитесь, что папка доступна для записи
2. Запустите от администратора (Power Shell от администратора)
3. Проверьте права доступа к папке
```

---

## ТЕХНИЧЕСКАЯ ПОДДЕРЖКА

### КОГДА ОБРАЩАТЬСЯ К ПОДДЕРЖКЕ

**Обращайтесь, если:**
- ❌ Система не компилируется
- ❌ API не отвечает
- ❌ Получаются неправильные результаты
- ❌ Система падает
- ✅ Есть идеи по улучшению

### ИНФОРМАЦИЯ ДЛЯ ПОДДЕРЖКИ

При обращении приложите:
1. Версию .NET: `dotnet --version`
2. Версию приложения
3. Операционную систему
4. Входное изображение (если возможно)
5. Лог ошибки (скопируйте текст из консоли)
6. Шаги для воспроизведения проблемы

### КОНТАКТНАЯ ИНФОРМАЦИЯ

```
Email: support@levelgenerator.dev
GitHub Issues: https://github.com/levelgenerator/issues
Discord: https://discord.gg/levelgenerator
```

---

## ДОПОЛНИТЕЛЬНЫЕ КОМАНДЫ

### РАЗРАБОТКА

```bash
# Запуск с отладкой
dotnet run --configuration Debug

# Сборка в Release
dotnet build -c Release

# Запуск тестов
dotnet test

# Публикация
dotnet publish -c Release -o ./publish
```

### DOCKER

```bash
# Сборка образа
docker build -t level-gen:latest .

# Запуск контейнера
docker run -p 5001:5001 level-gen:latest

# Просмотр логов
docker logs <container-id>

# Остановка контейнера
docker stop <container-id>
```

### ОЧИСТКА

```bash
# Удалить скомпилированные файлы
dotnet clean

# Удалить кэш NuGet
dotnet nuget locals all --clear

# Полная переустановка зависимостей
dotnet restore --force
```

---

## БЫСТРЫЕ ССЫЛКИ

| Файл | Назначение |
|------|-----------|
| README.md | Основная документация |
| API_EXAMPLES.md | Примеры использования API |
| COMPLETE_GUIDE.md | Полный guide |
| level.json | Выводные данные уровня |
| output.png | Обработанное изображение |

---

## РЕКОМЕНДАЦИИ

### ДЛЯ ЛУЧШИХ РЕЗУЛЬТАТОВ:

1. **Подготовка изображения:**
   - Используйте высокий контраст
   - Избегайте теней и отражений
   - Размер: 600x400 до 1920x1080 пиксель

2. **Оптимизация производительности:**
   - Закройте ненужные приложения
   - Используйте SSD для быстрой работы
   - Обрабатывайте большие файлы ночью

3. **Безопасность:**
   - Не делитесь конфиденциальными изображениями
   - Используйте HTTPS для API
   - Регулярно обновляйте систему

---

## ОБНОВЛЕНИЕ СИСТЕМЫ

```bash
# Проверка обновлений
git fetch origin
git status

# Обновление
git pull origin main

# Пересборка
dotnet clean
dotnet restore
dotnet build
```

---

## ПОДДЕРЖКА ФОРМАТОВ

### Поддерживаемые форматы изображений:
- ✅ PNG (рекомендуется)
- ✅ JPEG
- ✅ BMP
- ✅ GIF
- ✅ TIFF

### Выходные форматы:
- ✅ JSON (основной)
- 🔄 (Планируется: XML, YAML, CSV)

---

## ЛИЦЕНЗИЯ И УСЛОВИЯ ИСПОЛЬЗОВАНИЯ

✅ **Свободен для использования** согласно лицензии MIT

✅ **Коммерческое использование** разрешено

❌ **Модификация без указания автора** не разрешена

❌ **Ответственность:** Используйте на свой риск

---

## БЛАГОДАРНОСТИ

Спасибо за использование системы!
Ваша обратная связь помогает нам улучшаться.

---

**Версия документа:** 1.0
**Дата:** Апрель 2026
**Статус:** ✅ Актуально

