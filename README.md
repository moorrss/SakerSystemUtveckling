## Starta projektet

1. Kopiera `.env.example` till `.env` och fyll i egna värden.
2. Skapa certifikatet: `dotnet dev-certs https -ep ./certs/jensenonline.pfx -p <CERT_PASSWORD>`
3. Starta: `docker compose up --build -d`
4. Öppna https://localhost:8443
