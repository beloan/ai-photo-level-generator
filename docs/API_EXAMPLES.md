## Примеры использования API

### Пример 1: Загрузка изображения через cURL

```bash
curl -X POST \
  -F "imageFile=@path/to/image.png" \
  https://localhost:5001/api/levelgeneration/generate
```

**Ответ:**
```json
{
  "success": true,
  "data": {
    "Width": 600,
    "Height": 400,
    "Platforms": [...]
  },
  "platformCount": 245,
  "dimension": {
    "width": 600,
    "height": 400
  }
}
```

### Пример 2: Загрузка по URL

```bash
curl -X POST \
  -H "Content-Type: application/json" \
  -d '{"imageUrl":"https://example.com/level-blueprint.png"}' \
  https://localhost:5001/api/levelgeneration/generate-from-url
```

### Пример 3: Python клиент

```python
import requests
import json

# Загрузка файла
with open('image.png', 'rb') as f:
    files = {'imageFile': f}
    response = requests.post(
        'https://localhost:5001/api/levelgeneration/generate',
        files=files,
        verify=False  # Для локального тестирования
    )

level_data = response.json()['data']

# Сохранение результата
with open('generated_level.json', 'w') as f:
    json.dump(level_data, f, indent=2)
```

### Пример 4: JavaScript клиент

```javascript
const generateLevelFromFile = async (imageFile) => {
    const formData = new FormData();
    formData.append('imageFile', imageFile);

    const response = await fetch(
        'https://localhost:5001/api/levelgeneration/generate',
        {
            method: 'POST',
            body: formData
        }
    );

    const levelData = await response.json();
    return levelData.data;
};

const generateLevelFromUrl = async (imageUrl) => {
    const response = await fetch(
        'https://localhost:5001/api/levelgeneration/generate-from-url',
        {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ imageUrl })
        }
    );

    const levelData = await response.json();
    return levelData.data;
};
```

## Структура ответа API

```typescript
interface ApiResponse {
    success: boolean;
    data: {
        Width: number;
        Height: number;
        Platforms: Platform[];
    };
    platformCount: number;
    dimension: {
        width: number;
        height: number;
    };
}

interface Platform {
    Start: { X: number; Y: number };
    End: { X: number; Y: number };
    Height: number;
}
```

## Тестирование

### Запуск тестов

```bash
dotnet test
```

### Manual Testing

1. Запустите API:
```bash
dotnet run
```

2. Откройте браузер и перейдите к:
```
https://localhost:5001/api/levelgeneration/health
```

3. Используйте Postman или curl для тестирования endpoints

## Troubleshooting

### Проблема: "Image Format not supported"
**Решение:** Убедитесь, что используете поддерживаемые форматы (PNG, JPEG, BMP)

### Проблема: "HTTPS Certificate Error"
**Решение:** Для локального тестирования используйте `--insecure` флаг в curl или `verify=False` в Python

### Проблема: API не отвечает
**Решение:** 
1. Проверьте, запущен ли api (`dotnet run`)
2. Проверьте port в конфиге (по умолчанию 5001)
3. Посмотрите логи консоли для ошибок

## Performance Tips

- Обработка больших изображений (>2000px) может занять время
- Используйте меньший формат для быстрого прототипирования
- Кэшируйте результаты для одинаковых изображений
- Используйте асинхронные запросы для параллельной обработки

