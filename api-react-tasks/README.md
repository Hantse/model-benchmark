# api-react-tasks

Tester autonome, destiné à être poussé dans son propre dépôt Git. Le squelette est
compilable ; les tests de fonctionnalités sont volontairement rouges jusqu'à
résolution de PROMPT.md. 35 tests obligatoires, aucun skip autorisé.

Prérequis : SDK .NET 10.0.302 (latestPatch), Linux ou Windows.
Frontend : Node >=22.12 et npm, dépendances figées par package-lock.json.

```sh
dotnet restore Benchmark.slnx --locked-mode
dotnet build Benchmark.slnx --no-restore
dotnet test Benchmark.slnx --no-restore --logger "trx;LogFileName=acceptance.trx" --results-directory TestResults
```

```sh
npm --prefix frontend ci
npm --prefix frontend run build
npm --prefix frontend test
```

Essai manuel : configuration Jwt de FEATURES.md, puis dotnet run --project src/TaskApi --urls http://127.0.0.1:5080.
Dans un second terminal : npm --prefix frontend run dev.
Rapport .NET TRX : TestResults/acceptance.trx.
Rapport React JUnit : TestResults/frontend.xml.

Le manifest est le contrat d'évaluation v1. Le contrôleur fixe URL + commit ;
cloner ce commit dans un dossier neuf pour chaque tentative. Les tests visibles
servent aussi au développement ; l'évaluateur final reprend leurs versions
protégées. Ne publier aucune solution de référence dans l'historique de ce dépôt.
Les comptes et clés décrits sont des données synthétiques de benchmark.
