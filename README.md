# 🏛️ ОПЦИОН

> **WPF-приложение для проверки контрагентов по УНП, получения курсов валют НБ РБ и работы с государственной статистикой Белстата — с экспортом в PDF, Excel и CSV.**

---

## 📖 О проекте

**ОПЦИОН** — это десктопное WPF-приложение для автоматизации проверки контрагентов и анализа официальной статистической информации. Проект создан для использования в следственной и аналитической работе.

Приложение агрегирует данные из трёх официальных источников:

| Источник | API | Назначение |
|----------|-----|------------|
| **Министерство по налогам и сборам РБ** | `grp.nalog.gov.by` | Проверка контрагентов по УНП |
| **Национальный банк РБ** | `api.nbrb.by` | Курсы валют, динамика, история |
| **Белстат** | `dataportal.belstat.gov.by` | Государственная статистика |

---

## ✨ Основные возможности

### 🔍 Проверка контрагентов

- Поиск по УНП (9 цифр) с автодополнением из истории
- Поиск по Enter, валидация УНП
- Детальная информация: наименование, адрес, статус, инспекция МНС, даты
- Массовый поиск: ввод списка УНП вручную или загрузка из `.txt`, `.csv`, `.xlsx`
- Автоматическая перепроверка статусов при запуске с фиксацией изменений

### 💱 Курсы валют НБ РБ

- Курс одной валюты на выбранную дату
- Список всех валют на дату
- Динамика курса за период
- Поиск и фильтрация валют
- История запросов

### 📊 Государственная статистика (Белстат)

- Дерево рубрик с нумерацией (1, 1.1, 1.1.1)
- Живой поиск показателей (точный + нечёткий через FuzzySharp)
- Просмотр данных в таблице с сортировкой и фильтрацией
- **Окно интерпретации** для следственной работы:
  - Простая интерпретация: отклонение от среднего
  - Расширенная: тренды, скачки, аномалии >2σ
  - Автоматические рекомендации по типам показателей
- Экспорт только отфильтрованных данных с пометкой

### 📄 Экспорт

- **PDF** (QuestPDF): постраничный экспорт, логотип СК, подпись «Проверил следователь» на последней странице
- **Excel** (EPPlus): с цветовой палитрой важности
- **CSV**: с учётом фильтра
- Копирование ячеек двойным кликом

### 🎨 Интерфейс

- Единый стиль Следственного комитета (васильковый + жёлтый)
- Светлая и тёмная темы
- Bootstrap-иконки (MahApps.Metro.IconPacks)
- Сортировка и фильтрация во всех таблицах
- Адаптивные размеры окон

---

## 🛠️ Технологический стек

| Категория | Технологии |
|-----------|------------|
| **Платформа** | .NET 8, WPF |
| **Архитектура** | MVVM, DI (Microsoft.Extensions.Hosting) |
| **База данных** | SQLite (Microsoft.Data.Sqlite.Core) |
| **HTTP** | IHttpClientFactory + Polly (retry) |
| **PDF** | QuestPDF |
| **Excel** | EPPlus |
| **Поиск** | FuzzySharp (нечёткий поиск) |
| **Иконки** | MahApps.Metro.IconPacks (Bootstrap Icons) |
| **JSON** | Newtonsoft.Json |
| **Логирование** | Microsoft.Extensions.Logging |

---

## 🏗️ Архитектура проекта
NalogParserWPF/
├── Configuration/ # Настройки API и БД
├── Converters/ # XAML-конвертеры
├── Database/ # Репозитории SQLite
├── Docs/ # Документация
├── Helpers/ # Вспомогательные классы
├── Models/ # Модели данных
├── Services/ # Сервисы (API, экспорт, кэш)
├── ViewModels/ # ViewModels
├── Views/ # Дополнительные окна
├── App.xaml # Глобальные ресурсы и стили
├── App.xaml.cs # DI-контейнер
├── MainWindow.xaml # Главное окно (4 вкладки)
├── appsettings.json # Конфигурация
├── zaglav.ico # Иконка (рукопожатие)
└── PROJECT_CONTEXT.md # Контекст проекта для агента

text

---

## Быстрый старт

### Требования

- Windows 10/11 (x64)
- .NET 8 SDK (для сборки)
- Visual Studio 2022 (опционально)

### Сборка и запуск

# Клонировать репозиторий
git clone https://github.com/your-username/NalogParserWPF.git
cd NalogParserWPF

# Сборка
dotnet build NalogParserWPF.sln

# Запуск
dotnet run --project NalogParserWPF/NalogParserWPF.csproj
Портативная версия (релиз)
powershell
dotnet publish NalogParserWPF/NalogParserWPF.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -o ./Publish
Готовый opcion.exe (~150–200 МБ) лежит в папке Publish/. Скопируйте appsettings.json рядом с .exe.

### Конфигурация (appsettings.json)
json
{
  "ApiSettings": {
    "BaseUrl": "http://grp.nalog.gov.by/api/grp-public/data",
    "TimeoutSeconds": 30,
    "RetryCount": 3
  },
  "DatabaseSettings": {
    "ConnectionString": "Data Source=search_history.db"
  },
  "NbrbSettings": {
    "BaseUrl": "https://api.nbrb.by/exrates/"
  },
  "BelstatSettings": {
    "BaseUrl": "https://dataportal.belstat.gov.by/osids-public-api/",
    "TimeoutSeconds": 60
  },
  "SelectedApi": "Nalog",
  "Theme": "Light"
}
### Документация API
API Налоговой службы
text
GET http://grp.nalog.gov.by/api/grp-public/data?unp={УНП}&charset=UTF-8&type=json
API НБ РБ
text
GET https://api.nbrb.by/exrates/rates?ondate={дата}&periodicity=0
GET https://api.nbrb.by/exrates/rates/{ISO}?parammode=2
GET https://api.nbrb.by/exrates/rates/dynamics/{cur_id}
API Белстата
text
GET  .../rubric/rubricTreeNodeGet/0
GET  .../rubric/rubricIndicatorsGet/{code}
POST .../sdmx-api/indicator/values/SDMX-JSON/{code}
Подробнее: Docs/BELSTAT_API_RESEARCH.md

### Вклад в проект
#Fork репозитория

#Создайте ветку (git checkout -b feature/amazing-feature)

#Закоммитьте изменения (git commit -m 'Add amazing feature')

#Push (git push origin feature/amazing-feature)

#Откройте Pull Request

### Лицензия
Распространяется под лицензией MIT.

### Автор
Климято Павел Викторович

Институт СК, КОЦРПС

### Благодарности
QuestPDF — генерация PDF

EPPlus — работа с Excel

MahApps.Metro.IconPacks — иконки

FuzzySharp — нечёткий поиск

Polly — retry-политики

<div align="center">
Сделано с ❤️ для правоохранителей Республики Беларусь