# Public Order Aggregator

## Cel aplikacji

Aplikacja pobiera ogłoszenia zamówień publicznych z różnych źródeł, filtruje je pod kątem istotności i generuje raporty HTML z wybranymi ogłoszeniami.

## Funkcjonalności

### Zbieranie danych
- Pobiera ogłoszenia z BZP (Biuletyn Zamówień Publicznych)
- Sprawdza czy zamówienie już istnieje w bazie
- Może obsługiwać wiele źródeł jednocześnie

### Klasyfikacja
- Używa GPT-4 do oceny czy zamówienie jest istotne
- Przetwarza zamówienia partiami
- Ogranicza liczbę zapytań do OpenAI

### Podsumowania
- Tworzy krótkie podsumowania istotnych zamówień
- Czyści tekst HTML
- Wyciąga podstawowe informacje (organizator, lokalizacja, termin)

### Raporty
- Tworzy raporty HTML
- Pokazuje tylko nowe zamówienia od ostatniego raportu
- Otwiera raport w przeglądarce

## Struktura

```
├── PublicOrderAggregator.Domain/          # Modele i interfejsy
│   ├── Entities/                          # PublicOrder
│   ├── Interfaces/                        # Interfejsy serwisów
│   └── Models/DTOs/                       # Obiekty danych
│
├── PublicOrderAggregator.Infrastructure/  # Implementacje
│   ├── AI/                               # OpenAI
│   ├── DataSources/BZP/                  # BZP API
│   ├── Factories/                        # Fabryki obiektów
│   ├── Processors/                       # HTML, raporty
│   ├── Repositories/                     # Entity Framework
│   └── Services/                         # Główny serwis
│
└── PublicOrderAggregator.Console/        # Aplikacja
    └── Extensions/                       # Konfiguracja DI
```

### Główne komponenty

- **DataSourceFactory**: Tworzy źródła danych (BZP, etc.)
- **MultiSourceOrderProcessingService**: Główny serwis obsługujący cały proces
- **ClassificationService**: GPT-4 klasyfikacja
- **SummaryService**: GPT-4 podsumowania

## Technologie

- .NET 8
- Entity Framework Core + SQLite
- OpenAI GPT-4
- HttpClient

## Konfiguracja

### appsettings.json

```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key",
    "Model": "gpt-4o-mini"
  },
  "DataSources": {
    "BZP": {
      "Enabled": true,
      "BaseUrl": "https://ezamowienia.gov.pl/mo-board/api/v1",
      "DaysBack": 1,
      "PageSize": 500
    }
  }
}
```

## Użycie

### Uruchomienie aplikacji
```bash
dotnet run --project PublicOrderAggregator.Console
```

### Co robi aplikacja
1. Tworzy bazę danych SQLite
2. Pobiera ogłoszenia z BZP
3. Wyciąga tekst z HTML
4. GPT-4 ocenia czy zamówienie jest istotne
5. GPT-4 tworzy podsumowania istotnych zamówień
6. Generuje raport HTML
7. Otwiera raport w przeglądarce

### Pliki wynikowe
- `report_yyyyMMdd_HHmmss.html` - raport HTML
- `publicorders.db` - baza danych SQLite

## Rozszerzanie

Żeby dodać nowe źródło danych:
1. Zaimplementuj `IDataSource` i `IContentParser`
2. Zarejestruj w DI container
3. Dodaj do `DataSourceFactory`
4. Skonfiguruj w appsettings.json