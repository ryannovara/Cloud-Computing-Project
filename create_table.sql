CREATE USER [FARnovaraMidterm-uami] FROM EXTERNAL PROVIDER;
ALTER ROLE db_datareader ADD MEMBER [FARnovaraMidterm-uami];
ALTER ROLE db_datawriter ADD MEMBER [FARnovaraMidterm-uami];

CREATE TABLE Games (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Title NVARCHAR(200) NOT NULL,
    Upc NVARCHAR(50) NOT NULL UNIQUE,
    Data NVARCHAR(MAX)
);

SELECT * FROM Games;

