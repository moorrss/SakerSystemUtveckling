# JENSENOnline – POC Säker systemutveckling

## Starta projektet

1. Kopiera `.env.example` till `.env` och fyll i egna värden.
2. Skapa och lita på certifikatet:
```bash
   mkdir -p certs
   dotnet dev-certs https --trust
   dotnet dev-certs https -ep ./certs/jensenonline.pfx -p <CERT_PASSWORD>
```
3. Starta: `docker compose up --build -d`
4. Öppna https://localhost:8443

## Testkonton

| Roll | E-post | Lösenord |
|---|---|---|
| Admin | admin@jensenonline.se | Admin-Demo-2026! |
| Kund | kund@jensenonline.se | Kund-Demo-2026! |

## Säkerhetstester

Testerna finns i `JensenOnline.Api/JensenOnline.Api.http` och körs med REST Client i VS Code.
