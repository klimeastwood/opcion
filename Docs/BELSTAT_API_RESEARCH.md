# BELSTAT API RESEARCH

- **Дата разведки:** 2026-10-03
- **Base URL:** `https://dataportal.belstat.gov.by/osids-public-api`
- **Авторизация:** отсутствует (публичный API)
- **Локализация:** заголовок `Accept-Language: ru` (варианты: `ru`, `en`, `be`)
- **Метод:** реальные GET/POST-запросы (таймаут 10–20 с), байт-точное чтение ответов
- **Формат ответов:** JSON (`application/json`), часть эндпоинтов отдаёт `application/octet-stream` (SDMX) и ZIP

## Оглавление

1. [Проверенные эндпоинты (по заданию)](#проверенные-эндпоинты-по-заданию) — угаданные URL и Swagger: 404
2. [Найденные рабочие эндпоинты (расширенная разведка)](#найденные-рабочие-эндпоинты-расширенная-разведка) — 7 групп, с примерами ответов
3. [Сводная таблица всех найденных рабочих эндпоинтов](#сводная-таблица-всех-найденных-рабочих-эндпоинтов) — 14 эндпоинтов
4. [Выводы](#выводы)
5. [Рекомендации для будущей реализации вкладки «Госстатистика»](#рекомендации-для-будущей-реализации-вкладки-госстатистика)
6. [Примечания и ограничения исследования](#примечания-и-ограничения-исследования)


---

## Проверенные эндпоинты (по заданию)

### Список показателей

- **URL:** `https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/indicator/list/SDMX-JSON`
- **Статус:** 404
- **Ответ (первые 1000 символов):**
```json
{"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource sdmx-api/indicator/list/SDMX-JSON.","instance":"/osids-public-api/sdmx-api/indicator/list/SDMX-JSON"}
```

- **URL:** `https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/indicators/SDMX-JSON`
- **Статус:** 404
- **Ответ (первые 1000 символов):**
```json
{"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource sdmx-api/indicators/SDMX-JSON.","instance":"/osids-public-api/sdmx-api/indicators/SDMX-JSON"}
```

- **URL:** `https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/indicator/datastructure/SDMX-JSON`
- **Статус:** 404
- **Ответ (первые 1000 символов):**
```json
{"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource sdmx-api/indicator/datastructure/SDMX-JSON.","instance":"/osids-public-api/sdmx-api/indicator/datastructure/SDMX-JSON"}
```

> Итог: угаданных «списочных» SDMX-эндпоинтов не существует. Рабочий способ получить список показателей — см. раздел «Найденные рабочие эндпоинты» (`POST /indicator/indicatorSearch`).

### Список рубрик

- **URL:** `https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/rubric/list/SDMX-JSON`
- **Статус:** 404
- **Ответ (первые 1000 символов):**
```json
{"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource sdmx-api/rubric/list/SDMX-JSON.","instance":"/osids-public-api/sdmx-api/rubric/list/SDMX-JSON"}
```

- **URL:** `https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/rubrics/SDMX-JSON`
- **Статус:** 404
- **Ответ (первые 1000 символов):**
```json
{"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource sdmx-api/rubrics/SDMX-JSON.","instance":"/osids-public-api/sdmx-api/rubrics/SDMX-JSON"}
```

### Swagger

- **URL:** `https://dataportal.belstat.gov.by/osids-public-api/swagger-ui.html` — **Статус: 404**
  ```json
  {"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource swagger-ui.html.","instance":"/osids-public-api/swagger-ui.html"}
  ```
- **URL:** `https://dataportal.belstat.gov.by/osids-public-api/v3/api-docs` — **Статус: 404**
  ```json
  {"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource v3/api-docs.","instance":"/osids-public-api/v3/api-docs"}
  ```
- **URL:** `https://dataportal.belstat.gov.by/osids-public-api/swagger.json` — **Статус: 404**
  ```json
  {"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource swagger.json.","instance":"/osids-public-api/swagger.json"}
  ```
- Дополнительно проверено (все **404**):
  `swagger-ui/index.html`, `v3/api-docs/swagger-config`, `swagger-resources`, `v2/api-docs`,
  `actuator`, `actuator/health`, `actuator/mappings`.

> Итог: Swagger/OpenAPI **не опубликован**. Официальная документация API встроена в сам API — см. `indicatorApiInfoGet`.

### Известные эндпоинты

- **URL:** `GET https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/indicator/datastructure/SDMX-JSON/10103000003`
- **Статус:** 200
- **Content-Type:** `application/octet-stream` (внутри — JSON), **размер:** 235 657 байт
- **Допустимые форматы файла:** только `SDMX-ML` и `SDMX-JSON` (регистрозависимо; `sdmx-json`, `SDMX-CSV`, `XLSX` → 400 `Запрошенный формат файла некорректен, корректные форматы: SDMX-ML, SDMX-JSON`)
- **Ответ (первые 1000 символов):**
```json
{
  "data" : {
    "dataflows" : [ {
      "id" : "10103000003_DATA_FLOW",
      "urn" : "urn:sdmx:org.sdmx.infomodel.datastructure.Dataflow=BELSTAT:10103000003_DATA_FLOW(1.0)",
      "links" : [ {
        "rel" : "self",
        "urn" : "urn:sdmx:org.sdmx.infomodel.datastructure.Dataflow=BELSTAT:10103000003_DATA_FLOW(1.0)",
        "type" : "dataflow"
      }, {
        "rel" : "structure",
        "urn" : "urn:sdmx:org.sdmx.infomodel.datastructure.DataStructure=BELSTAT:10103000003_STRUCTURE(1.0)",
        "type" : "datastructure"
      } ],
      "version" : "1.0",
      "agencyID" : "BELSTAT",
      "name" : "Number of places in preschool education  institutions",
      "names" : {
        "en" : "Number of places in preschool education  institutions",
        "ru" : "Число мест в учреждениях дошкольного образования",
        "be" : "Колькасць месцаў ва ўстановах дашкольнай адукацыі"
      },
      "annotations" : [ ],
      "structure" : "urn:sdmx:org.sdmx.infomodel.datastructure.D
```

- **URL:** `GET https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/rubric/indicators/datastructure/SDMX-JSON/10613`
- **Статус:** 200
- **Content-Type:** `application/octet-stream`, **размер:** 821 507 байт
- **Формат:** **ZIP-архив** (сигнатура `PK\x03\x04`). Внутри — по одному файлу структуры на показатель, имя вида `10103000022_sdmx_structure.json`.
- **Ответ (первые 1000 символов, текст как приходит — бинарный ZIP):**
```text
PK.. 10103000022_sdmx_structure.json ... (бинарные данные архива, декодированию в текст не подлежат)
```

---

## Найденные рабочие эндпоинты (расширенная разведка)

Base: `https://dataportal.belstat.gov.by/osids-public-api` (без авторизации, заголовок `Accept-Language: ru`).

> Источник: сайт `https://dataportal.belstat.gov.by` — Angular-приложение (SPA), база API задана в бандле как `SERVER_URL = "/osids-public-api"`. Полный перечень маршрутов восстановлен из клиентского кода (`main.*.js`, lazy-chunks) и подтверждён живыми запросами. Плюс сам API само-документируется через `indicatorApiInfoGet` (см. п. 5).

### 1. Список показателей (полный каталог) — найден

- **URL:** `POST https://dataportal.belstat.gov.by/osids-public-api/indicator/indicatorSearch`
- **Заголовки:** `Content-Type: application/json`, `Accept-Language: ru`
- **Тело:** `{}` — вернуть все показатели; `{"name":"инфляция"}` — фильтр по названию/ключевым словам
- **Статус:** 200 · `application/json` · **≈ 246 132 байта** (весь каталог)
- **Ответ (первые 1000 символов, тело `{}`):**
```json
{"items":[{"code":"10101200022","name":"Число умерших детей в возрасте до 5 лет","key_words":null,"periodicities":["годовая"],"nextPublicationDate":"2026-04-28T00:00:00.000+00:00"},{"code":"10101200019","name":"Число умерших детей","key_words":null,"periodicities":["годовая"],"nextPublicationDate":"2026-04-28T00:00:00.000+00:00"},{"code":"10109200008","name":"Число спортивно-массовых мероприятий с участием детей","key_words":null,"periodicities":["годовая"],"nextPublicationDate":"2026-04-24T00:00:00.000+00:00"},{"code":"10101100011","name":"Число семей с детьми в возрасте до 18 лет","key_words":null,"periodicities":["годовая"],"nextPublicationDate":"2021-08-19T00:00:00.000+00:00"},{"code":"10109100019","name":"Число проведенных театрами утренних мероприятий","key_words":null,"periodicities":["годовая"],"nextPublicationDate":"2026-04-17T00:00:00.000+00:00"},{"code":"10101100017","name":"Число населенных пунктов, расположенных в зонах радиоактивного загрязнения","key_words":null,"periodi
```

- **Пример фильтра:** тело `{"name":"инфляция"}` → 200, вернул 2 показателя:
```json
{"items":[{"code":"10217100003","name":"Индекс потребительских цен на товары и услуги в % к предыдущему периоду","key_words":"индекс цен, инфляция, индекс потребительских цен, ИПЦ","periodicities":["годовая","месячная"],"nextPublicationDate":"2026-09-10T00:00:00.000+00:00"},{"code":"10217100004","name":"Индекс потребительских цен на товары и услуги в % к декабрю предыдущего года","key_words":"индекс цен, инфляция, индекс потребительских цен, ИПЦ","periodicities":["годовая","месячная"],"nextPublicationDate":"2026-09-10T00:00:00.000+00:00"}]}
```

### 2. Показатели внутри рубрики — найден

- **URL:** `GET https://dataportal.belstat.gov.by/osids-public-api/rubric/rubricIndicatorsGet/{rubricCode}`
- **Пример:** `.../rubric/rubricIndicatorsGet/10613` («Статистика образования»)
- **Статус:** 200 · `application/json` · ≈ 6–8 КБ
- **Ответ (первые 1000 символов):**
```json
{"items":[{"code":"10103000001","name":"Число учреждений образования","periodicities":["годовая"],"nextPublicationDate":"2026-05-19T00:00:00.000+00:00","specialDescription":null},{"code":"10103000002","name":"Численность обучающихся в учреждениях образования","periodicities":["годовая"],"nextPublicationDate":"2026-05-19T00:00:00.000+00:00","specialDescription":null},{"code":"10103000003","name":"Число мест в учреждениях дошкольного образования","periodicities":["годовая"],"nextPublicationDate":"2026-02-03T00:00:00.000+00:00","specialDescription":null},{"code":"10103000004","name":"Численность детей, приходящихся на 100 мест в учреждениях дошкольного образования","periodicities":["годовая"],"nextPublicationDate":"2026-02-03T00:00:00.000+00:00","specialDescription":null},{"code":"10103000005","name":"Количество групп в учреждениях дошкольного образования ","periodicities":["годовая"],"nextPublicationDate":"2026-02-03T00:00:00.000+00:00","specialDescription":null},{"code":"10103000006","n
```
- **Родственный:** `GET .../rubric/rubricMainIndicatorsGet/{rubricCode}` → 200, главные показатели рубрики (HTML-витрина, ≈ 1 КБ)

### 3. Список рубрик (дерево) — найден

- **URL:** `GET https://dataportal.belstat.gov.by/osids-public-api/rubric/rubricTreeNodeGet/{rubricCode}` (`0` = корень → всё дерево)
- **Статус:** 200 · `application/json` · ≈ 10,7 КБ
- **Ответ (первые 1000 символов):**
```json
{"item":{"code":"0","name":null,"iconId":null,"imageId":null,"children":[{"code":"1063066","name":"Демографическая и социальная статистика","iconId":"rubric_icon_1063066","imageId":"rubric_image_1063066","children":[{"code":"1063260","name":"Статистика населения и миграции","iconId":"rubric_icon_1063260","imageId":"rubric_image_1063260","children":[{"code":"1063261","name":"Статистика населения","iconId":"rubric_icon_1063261","imageId":"rubric_image_1063261","children":[]},{"code":"1063262","name":"Статистика естественного движения населения","iconId":"rubric_icon_1063262","imageId":"rubric_image_1063262","children":[]},{"code":"1063263","name":"Статистика миграции населения","iconId":"rubric_icon_1063263","imageId":"rubric_image_1063263","children":[]}]},{"code":"1060402","name":"Статистика труда","iconId":"rubric_icon_1060402","imageId":"rubric_image_1060402","children":[]},{"code":"10613","name":"Статистика образования","iconId":"rubric_icon_10613","imageId":"rubric_image_10613","ch
```
- **Важно:** рубрика `10613` = «Статистика образования», рубрика `1063066` = «Демографическая и социальная статистика».
- **Родственные:** `GET .../rubric/rubricNavigationGet/{code}` (200, `{"item":null}`), `GET .../thematicSection/thematicSectionTreeNodeGet/0` (200, ≈ 11 КБ — дерево тематических разделов).

### 4. Метаданные рубрики — найден

- **URL:** `POST https://dataportal.belstat.gov.by/osids-public-api/rubric/rubricMetadataGet`
- **Тело:** строка с кодом рубрики, `Content-Type: text/plain` → `10613`
- **Статус:** 200 · `application/json` · ≈ 19 КБ
- **Ответ (первые 1000 символов):**
```json
{"rubricName":"Статистика образования\nСправочные метаданные в структуре метаданных Euro SDMX (ESMS) \nНабор данных: ESMS-103-01","metadata":[{"code":"1","title":"Контактная информация","value":null,"children":[{"code":"1.1","title":"Организация","value":"Национальный статистический комитет Республики Беларусь"},{"code":"1.2","title":"Структурное подразделение","value":"Главное управление статистики услуг и внутренней торговли"},{"code":"1.5","title":"Адрес электронной почты","value":"chigireva@belstat.gov.by"}]},{"code":"2","title":"Обновление метаданных","value":null,"children":[{"code":"2.1","title":"Последняя сертификация метаданных","value":"22.04.2025"},{"code":"2.2","title":"Последние опубликованные метаданные","value":"22.04.2025"},{"code":"2.3","title":"Последнее обновление метаданных","value":"22.04.2025"}]},{"code":"3","title":"Статистическое представление","value":null,"children":[{"code":"3.1","title":"Описание данных","value":"статистика образования – отрасль демографичес
```
- **Примечания:** `GET` на этот URL → **405** (`Method 'GET' is not supported`); тело как JSON-объект (`{"rubricCode":"10613"}`) → **400**.
- **XML/JSON-выгрузки метаданных рубрики (GET, 200):** `.../rubric/rubricMetadataSdmxJsonGet/10613` (текстовый дамп метаданных ESMS).

### 5. Официальный SDMX-API (само-документируемый) — найден

Ключевая находка: эндпоинт `indicatorApiInfoGet` возвращает **официальные публичные URL** SDMX-API для показателя. Он же и есть «встроенная документация» вместо отсутствующего Swagger.

- **URL:** `POST https://dataportal.belstat.gov.by/osids-public-api/indicator/indicatorApiInfoGet`
- **Тело:** `{"indicatorCode":"10103000003","valuesFilter":null}` · `Content-Type: application/json`
- **Статус:** 200 · `application/json` · 662 байта · **Ответ (полностью):**
```json
{"indicatorCode":"10103000003","indicatorName":"Число мест в учреждениях дошкольного образования","indDatastructureApiUrl":"GET https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/indicator/datastructure/{fileFormat}/10103000003","indValuesApiUrl":"POST https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/indicator/values/{fileFormat}/10103000003","rubricIndicatorsDatastructureApiUrl":"GET https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/rubric/indicators/datastructure/{fileFormat}/10613","rubricIndicatorsValuesApiUrl":"POST https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/rubric/indicators/values/{fileFormat}/10613"}
```
> `{fileFormat}` ∈ `SDMX-ML` | `SDMX-JSON` (регистрозависимо; другие значения → 400).

#### 5.1 Структура показателя (datastructure) — GET
| URL | Статус | Тип / размер |
|---|---|---|
| `GET .../sdmx-api/indicator/datastructure/SDMX-JSON/10103000003` | **200** | JSON, 235 657 байт |
| `GET .../sdmx-api/indicator/datastructure/SDMX-ML/10103000003` | **200** | XML, 169 080 байт |
| `GET .../sdmx-api/indicator/datastructure/SDMX-CSV/10103000003` | 400 | `Запрошенный формат файла некорректен, корректные форматы: SDMX-ML, SDMX-JSON` |
| `GET .../sdmx-api/indicator/datastructure/XLSX/10103000003` | 400 | то же |
| `GET .../sdmx-api/indicator/datastructure/SDMX-JSON/99999999999` | 500 | несуществующий код показателя |

#### 5.2 Значения показателя (values) — POST
- **URL:** `POST https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/indicator/values/SDMX-JSON/10103000003`
- **Тело (обязательно!):** `{"years":[2023]}` — год числами или строками; без `years` → 400 `Период (годы) не указан.`
- **Статус:** 200 · `application/json` · ≈ 10 079 байт (за 1 год) / ≈ 23 944 байта (за 2019–2023)
- **Ответ (первые 1000 символов, `{"years":[2023]}`):**
```json
{"header":{"id":"dbc40641-9cf4-499f-9a1e-1b90bc956bff","prepared":"2026-10-03T20:49:36","test":false,"sender":{"id":"unknown","name":"unknown"}},"dataSets":[{"action":"Information","observations":{"0:0:0:-1":["2096.0"],"1:0:0:-1":["438.0"],"2:0:0:-1":["1396.0"],"3:0:0:-1":["4935.0"],"4:1:0:-1":["50474.0"],"5:0:0:-1":["7915.0"],"6:0:0:-1":["1057.0"],"7:0:0:-1":["5384.0"],"8:0:0:-1":["18308.0"],"9:0:0:-1":["2703.0"],"10:0:0:-1":["445.0"],"11:0:0:-1":["40538.0"],"12:0:0:-1":["7882.0"],"13:0:0:-1":["598.0"],"14:0:0:-1":["730.0"],"15:0:0:-1":["1066.0"],"16:0:0:-1":["729.0"],"17:0:0:-1":["943.0"],"18:0:0:-1":["435.0"],"19:1:0:-1":["10184.0"],"20:0:0:-1":["701.0"],"21:0:0:-1":["4110.0"],"22:0:0:-1":["467.0"],"23:0:0:-1":["1015.0"],"24:0:0:-1":["1002.0"],"25:0:0:-1":["1680.0"],"26:0:0:-1":["1285.0"],"27:0:0:-1":["585.0"],"28:0:0:-1":["1003.0"],"29:0:0:-1":["2575.0"],"30:0:0:-1":["1114.0"],"31:0:0:-1":["3952.0"],"32:0:0:-1":["599.0"],"33:0:0:-1":["374.0"],"34:0:0:-1":["648.0"],"35:0:0:-1":["846
```

#### 5.3 Значения всех показателей рубрики (values) — POST
- **URL:** `POST https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/rubric/indicators/values/SDMX-JSON/10613`
- **Тело:** `{"years":[2023]}`
- **Статус:** 200 · **ZIP-архив**, 50 732 байта. Внутри — по одному файлу на показатель: `10103000022_sdmx_values.json`, `10103000020_sdmx_values.json`, …
- **Формат `SDMX-ML`** для rubric datastructure/values — по аналогии (проверен вариант datastructure `SDMX-ML` для показателя).

#### 5.4 Структура всех показателей рубрики (datastructure) — GET
- **URL:** `GET https://dataportal.belstat.gov.by/osids-public-api/sdmx-api/rubric/indicators/datastructure/SDMX-JSON/10613`
- **Статус:** 200 · **ZIP-архив** (`PK\x03\x04`), 821 507 байт. Внутри — `10103000022_sdmx_structure.json`, …

### 6. Карточка показателя и его описание — найдены

- **`GET .../indicator/indicatorGet/10103000003`** → 200 · `application/json` · 206 байт (полностью):
```json
{"item":{"code":"10103000003","name":"Число мест в учреждениях дошкольного образования","periodicities":null,"nextPublicationDate":null,"specialDescription":null}}
```
- **`GET .../indicator/indicatorDescriptionGet/10103000003`** → 200 · `application/json` · 219 байт (полностью):
```json
[{"code":"3.1","title":"Описание данных","value":"Данные приводятся о фактическом числе мест в учреждениях дошкольного образования."}]
```
- **`GET .../rubric/rubricMetadataSdmxJsonGet/10613`** → 200 · `application/octet-stream` (UTF-8 текст с BOM) · 27 352 байта. Дамп метаданных ESMS в виде пар `код Название;Значение` (первые 1000 символов):
```text
1 Контактная информация 1.1 Организация;Национальный статистический комитет Республики Беларусь 1.2 Структурное подразделение;Главное управление статистики услуг и внутренней торговли 1.5 Адрес электронной почты;chigireva@belstat.gov.by 2 Обновление метаданных 2.1 Последняя сертификация метаданных;22.04.2025 2.2 Последние опубликованные метаданные;22.04.2025 2.3 Последнее обновление метаданных;22.04.2025 3 Статистическое представление 3.1 Описание данных;статистика образования – отрасль демографической и социальной статистики, изучающая количественные и качественные явления и процессы, происходящие в сфере образования Основные статистические показатели: число учреждений образования; численность обучающихся в учреждениях образования; число мест в учреждениях дошкольного образования; численность детей, приходящихся на 100 мест в учреждениях дошкольного образования; количество групп в учреждениях дошкольного образования; количество групп кратковременного пребывания детей; числ
```
- **`GET .../rubric/rubricMainIndicatorsGet/10613`** → 200 · `application/json` · 1 189 байт — витрина «главных» показателей рубрики (`html`-фрагменты + имена PNG). Первые 1000 символов:
```json
[{"id":54,"serialNumber":1,"fileName":"rubric_main_indicator_ru7c4a54e7-3377-3161-9ec1-389be6607b45_1716803870300.png","html":"<span style=\"width: 80%; font-weight: bold; font-size: 13px; color: rgb(0, 91, 100);\">около 7 тысяч</span>\r\n<span style=\"width: 70%; font-size: 8px; color: rgb(0, 91, 100);\">УЧРЕЖДЕНИЙ ОБРАЗОВАНИЯ (2025/2026)</span>"},{"id":55,"serialNumber":2,"fileName":"rubric_main_indicator_rucf857f24-dfe9-3334-a400-4ce6d3421aaa_1716803920327.png","html":"<span style=\"width: 80%; font-weight: bold; font-size: 13px; color: rgb(0, 91, 100);\">1,8 млн</span>\r\n<span style=\"width: 70%; font-size: 8px; color: rgb(0, 91, 100);\">ОБУЧАЮЩИХСЯ В УЧРЕЖДЕНИЯХ ОБРАЗОВАНИЯ (2025/2026)</span>"},{"id":56,"serialNumber":3,"fileName":"rubric_main_indicator_ru7c2d3517-6970-3cf5-85b9-3a0bad3997aa_1716803956943.png",...
```

### 7. Проверенные «мимо»-эндпоинты (404) — чтобы не тратить время

| URL | Статус |
|---|---|
| `GET .../rubric/rubricGet/10613` | 404 `{"type":"about:blank","title":"Not Found","status":404,"detail":"No static resource rubric/rubricGet/10613.",...}` |
| `GET .../rubric/rubricDescriptionGet/10613` | 404 (аналогично) |
| `GET .../rubric/rubricNavigationGet/10613` | 200, но `{"item":null}` — пустышка |

---

## Сводная таблица всех найденных рабочих эндпоинтов

| № | Метод | Путь (после `/osids-public-api`) | Тело запроса | Ст. | Content-Type | Размер | Назначение |
|---|---|---|---|---|---|---|---|
| 1 | GET | `/rubric/rubricTreeNodeGet/0` | — | 200 | `application/json` | ~10,7 КБ | дерево рубрик (весь каталог) |
| 2 | GET | `/thematicSection/thematicSectionTreeNodeGet/0` | — | 200 | `application/json` | ~11 КБ | дерево тематических разделов |
| 3 | GET | `/rubric/rubricIndicatorsGet/{rubricCode}` | — | 200 | `application/json` | 6–8 КБ | показатели рубрики |
| 4 | GET | `/rubric/rubricMainIndicatorsGet/{rubricCode}` | — | 200 | `application/json` | ~1,2 КБ | «главные» показатели (html-витрина) |
| 5 | GET | `/indicator/indicatorGet/{code}` | — | 200 | `application/json` | ~206 Б | краткая карточка показателя |
| 6 | GET | `/indicator/indicatorDescriptionGet/{code}` | — | 200 | `application/json` | ~219 Б | описание показателя |
| 7 | POST | `/indicator/indicatorSearch` | `{}` или `{"name":"..."}` | 200 | `application/json` | ~246 КБ | **полный каталог показателей / поиск** |
| 8 | POST | `/indicator/indicatorApiInfoGet` | `{"indicatorCode":"...","valuesFilter":null}` | 200 | `application/json` | ~662 Б | **официальные URL SDMX-API** (self-doc) |
| 9 | POST | `/rubric/rubricMetadataGet` | `text/plain` = код рубрики | 200 | `application/json` | ~19 КБ | метаданные рубрики (ESMS) |
| 10 | GET | `/rubric/rubricMetadataSdmxJsonGet/{rubricCode}` | — | 200 | `application/octet-stream` | ~27 КБ | метаданные ESMS текстом |
| 11 | GET | `/sdmx-api/indicator/datastructure/{fmt}/{code}` | — | 200 | JSON/XML | 236 КБ / 169 КБ | структура (DataStructure) показателя |
| 12 | POST | `/sdmx-api/indicator/values/{fmt}/{code}` | `{"years":[2023]}` | 200 | `application/json` | ~10 КБ | **значения показателя** |
| 13 | GET | `/sdmx-api/rubric/indicators/datastructure/{fmt}/{rubricCode}` | — | 200 | **ZIP** | ~822 КБ | структуры всех показателей рубрики |
| 14 | POST | `/sdmx-api/rubric/indicators/values/{fmt}/{rubricCode}` | `{"years":[2023]}` | 200 | **ZIP** | ~51 КБ | значения всех показателей рубрики |

`{fmt}` ∈ `SDMX-JSON` \| `SDMX-ML`.

---

## Выводы

1. **Публичный API существует, авторизация не требуется.** Всё, что нужно для вкладки «Госстатистика», доступно анонимно с заголовком `Accept-Language: ru` (есть также `en`, `be`).
2. **Swagger / OpenAPI не публикуется.** `/swagger-ui/**`, `/v3/api-docs/**`, `/api-docs`, `/actuator/**` → 404. Документацию пришлось восстанавливать из JS-бандла Angular-приложения и подтверждать живыми запросами.
3. **API само-документируется точечно:** `indicatorApiInfoGet` по коду показателя отдаёт официальные URL SDMX-API (datastructure/values для показателя и для его рубрики). Это лучший «справочник» для формирования запросов на этапе реализации.
4. **Данные отдаются через SDMX.** Формат ответа — стандартный SDMX-JSON: `header` + `dataSets[].observations`, где ключ наблюдения — индексная строка `0:0:0:-1` (размерности — из `structure`-ответа), а значение — массив строк (`["2096.0"]`). Для нормализации в таблицу нужно склеить `datastructure` (расшифровка измерений) и `values`.
5. **Ограничение форматов:** поддерживаются ровно `SDMX-ML` и `SDMX-JSON`, регистрозависимо. `SDMX-CSV`, `XLSX`, `sdmx-json` → 400 с сообщением `Запрошенный формат файла некорректен, корректные форматы: SDMX-ML, SDMX-JSON`.
6. **Параметр `years` обязателен для `values`.** POST без тела/`years` → 400 `Период (годы) не указан.` Годы можно передавать числами: `{"years":[2019,2020,2021,2022,2023]}` (проверено, ответ ≈ 23,9 КБ).
7. **Рубричные «пакетные» выгрузки возвращают ZIP, а не JSON.** `/sdmx-api/rubric/indicators/{datastructure|values}/{fmt}/{rubricCode}` → `application/octet-stream` с ZIP-сигнатурой; внутри по файлу на показатель (`{indicatorCode}_sdmx_values.json`). Это удобный способ забрать всю рубрику одним запросом, но требует распаковки `System.IO.Compression.ZipArchive`.
8. **Каталог показателей — порядка 246 КБ JSON** (`indicatorSearch` с `{}`), включает `code`, `name`, `key_words`, `periodicities`, `nextPublicationDate`. Этого достаточно для локального поиска/автодополнения без обращения к SDMX.
9. **Ошибки у API «человеческие»:** 400 с текстовым сообщением на русском, 500 на несуществующий код показателя, 404 (Spring `application/problem+json`) на несуществующий маршрут. Отдельного поля/формата кодов ошибок нет.
10. **Стабильность/сетевые особенности:** запросы по HTTPS работают напрямую, TLS валиден; таймаут 20–25 с достаточен даже для ZIP на 822 КБ.

---

## Рекомендации для будущей реализации вкладки «Госстатистика»

1. **Слои доступа (по образцу `NBRBApiService`):** создать `Services/BelstatApiService.cs` с единственным `HttpClient`, `BaseAddress = https://dataportal.belstat.gov.by/osids-public-api/`, заголовок `Accept-Language: ru` (настраиваемый), таймаут 30 с. Отдельный DTO-слой для SDMX (`BelstatSdmxHeader`, `BelstatSdmxDataSet`, `BelstatSdmxStructure`).
2. **Навигация по вкладке:**
   - дерево рубрик ← `GET rubric/rubricTreeNodeGet/0` (кэшировать в памяти/на диск — меняется редко);
   - список показателей рубрики ← `GET rubric/rubricIndicatorsGet/{code}`;
   - локальный поиск/фильтр ← `POST indicator/indicatorSearch` с `{"name":"..."}`; при старте загрузить один раз `{}` и фильтровать локально — быстрее и гуманнее к API.
3. **Получение данных показателя:**
   - `POST sdmx-api/indicator/values/SDMX-JSON/{code}` с телом `{"years":[<годы>]}`;
   - при первом обращении к показателю — `GET sdmx-api/indicator/datastructure/SDMX-JSON/{code}` и закэшировать расшифровку измерений (структура меняется редко, values — часто);
   - собирать «плоскую» таблицу (`DataTable`) из `observations` + словарей измерений и выводить в `DataGrid` с экспортом (по аналогии с существующим экспортом приложения).
4. **Массовая выгрузка по рубрике:** `POST sdmx-api/rubric/indicators/values/SDMX-JSON/{rubricCode}` → распаковать ZIP через `System.IO.Compression.ZipArchive` и объединить файлы. Показывать прогресс (операция «тяжёлая»: 50–800 КБ и более).
5. **Кэширование и вежливость:** кэш каталога рубрик/показателей на диск (JSON рядом с настройками), ограничение одновременных запросов, использование `nextPublicationDate` для подсказки «когда ждать обновление».
6. **Обработка ошибок:** различать 400 (некорректный код/формат/`years`), 404 (неверный маршрут), 500 (код показателя не найден) и сетевые таймауты; показывать пользователю текст ответа (`detail`).
7. **Конфигурация:** добавить секцию `Belstat` (`BaseUrl`, `Language`) в `appsettings.json` по аналогии с существующими секциями; не хардкодить URL.

---

## Примечания и ограничения исследования

- Разведка выполнялась **внешними PowerShell-скриптами** (`Docs/_belstat_probe*.ps1`); код приложения не изменялся и не пересобирался.
- Body-контракты POST-эндпоинтов, кроме явно показанных (`indicatorSearch`, `indicatorApiInfoGet`, `rubricMetadataGet`, `.../values/{fmt}/{code}`), установлены по фактическим успешным вызовам; DTO-схемы сервером не публикуются.
- Проверка выполнялась `2026-10-03`; состав каталога показателей и даты публикаций меняются — размеры ответов указаны как ориентир.
- PowerShell 5.1 некорректно передаёт кириллицу в литералах: вспомогательные скрипты написаны в ASCII, результаты складывались в JSON-файл и читались как UTF-8.
- **Уборка артефактов:** скачанные JS-бандлы `_belstat_main.js` (1,4 МБ), `_belstat_runtime.js`, каталог `_belstat_chunks/` и промежуточные `_belstat_probe*_result.json` удалены (освобождено ≈ 2,9 МБ). Небольшие скрипты разведки `Docs/_belstat_probe*.ps1`, `_belstat_chunks_probe.ps1`, `_belstat_urls_probe*.ps1` оставлены как воспроизводимое доказательство проведённых проверок; при необходимости их можно удалить одной командой:
  ```powershell
  Remove-Item Docs\_belstat_*.ps1 -Force
  ```






