//Wersja DotNeta 9
using System.Data.SqlClient;

class Program
{
    [Obsolete]
    static void Main()
    {
        string connectionString = @"Server=.\SQLEXPRESS;Integrated Security=true;";
        string databaseName = "Fazmis";
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();

            string createDbQuery = $@"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
            BEGIN
                CREATE DATABASE {databaseName}
            END";

            ExecuteQuery(connection, createDbQuery);
        }

        string dbConnectionString = $@"Server=.\SQLEXPRESS;Database={databaseName};Integrated Security=true;";

        using (SqlConnection connection = new SqlConnection(dbConnectionString))
        {
            connection.Open();

            string query = @"

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Kategorie')
            CREATE TABLE Kategorie (
                ID_Kategorii INT PRIMARY KEY IDENTITY(1,1),
                Nazwa VARCHAR(100) NOT NULL,
                Typ VARCHAR(50) NOT NULL
            );

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Dostawcy')
            CREATE TABLE Dostawcy (
                ID_Dostawcy INT PRIMARY KEY IDENTITY(1,1),
                Nazwa VARCHAR(255) NOT NULL,
                Telefon VARCHAR(20),
                Email VARCHAR(255),
                Ulica VARCHAR(100),
                Nr_Budynku VARCHAR(10),
                Nr_Lokalu VARCHAR(10),
                Kod_Pocztowy VARCHAR(10),
                Miasto VARCHAR(100),
                Kraj VARCHAR(100)
            );

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Magazyn')
            CREATE TABLE Magazyn (
                ID_Przedmiotu INT PRIMARY KEY IDENTITY(1,1),
                Nazwa VARCHAR(255) NOT NULL,
                Data_Waznosci DATE NULL,
                Ilosc DECIMAL(10,4) NOT NULL,
                Jednostka VARCHAR(50),
                Dostepny BIT NOT NULL,
                ID_Kategorii INT NOT NULL,
                FOREIGN KEY (ID_Kategorii) REFERENCES Kategorie(ID_Kategorii)
            );

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Zamowienia')
            CREATE TABLE Zamowienia (
                ID_Zamowienia INT PRIMARY KEY IDENTITY(1,1),
                ID_Dostawcy INT NOT NULL,
                Data_Zamowienia DATE NOT NULL,
                Status VARCHAR(50) NOT NULL,
                FOREIGN KEY (ID_Dostawcy) REFERENCES Dostawcy(ID_Dostawcy)
            );

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Pozycje_Zamowienia')
            CREATE TABLE Pozycje_Zamowienia (
                ID_Pozycji INT PRIMARY KEY IDENTITY(1,1),
                ID_Zamowienia INT NOT NULL,
                ID_Przedmiotu INT NOT NULL,
                Ilosc_Zamowiona DECIMAL(10,4) NOT NULL,
                Cena_Jednostkowa DECIMAL(10,2) NOT NULL,
                Data_Dostawy DATE NOT NULL,
                FOREIGN KEY (ID_Zamowienia) REFERENCES Zamowienia(ID_Zamowienia),
                FOREIGN KEY (ID_Przedmiotu) REFERENCES Magazyn(ID_Przedmiotu)
            );

            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Zuzycie')
            CREATE TABLE Zuzycie (
                Id_Zuzycia INT PRIMARY KEY IDENTITY(1,1),
                Data_Zuzycia DATE NOT NULL,
                Typ_Zuzycia VARCHAR(50) NOT NULL,
                Ilosc_Zuzycia DECIMAL(10,4),
                ID_Przedmiotu INT NOT NULL,
                FOREIGN KEY (ID_Przedmiotu) REFERENCES Magazyn(ID_Przedmiotu)
            );
            ";

            ExecuteQuery(connection, query);

            Console.WriteLine("Sukces");
        }
    }

    static void ExecuteQuery(SqlConnection connection, string query)
    {
        using (SqlCommand command = new SqlCommand(query, connection))
        {
            command.ExecuteNonQuery();
        }
    }
}