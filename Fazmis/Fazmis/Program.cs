//Wersja DotNeta 9
using System;
using Microsoft.Data.SqlClient; // Zmieniłem pakiet ponieważ poprzedni powodował błędy 
class Program
{
    static void Main()
    {
        string serverConnection = @"Server=.\SQLEXPRESS;Integrated Security=true;TrustServerCertificate=True;";
        string databaseName = "Fazmis";
        string dbConnectionString = $@"Server=.\SQLEXPRESS;Database={databaseName};Integrated Security=true;TrustServerCertificate=True;";

        // 1. Tworzenie Bazy Danych
        using (SqlConnection connection = new SqlConnection(serverConnection))
        {
            connection.Open();
            string createDbQuery = $@"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'{databaseName}')
            BEGIN
                CREATE DATABASE {databaseName}
            END";
            ExecuteQuery(connection, createDbQuery);
        }

        using (SqlConnection connection = new SqlConnection(dbConnectionString))
        {
            connection.Open();

            string createTablesQuery = @"
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
            );";

            ExecuteQuery(connection, createTablesQuery);
            string DanePrzyklad = @"
            IF NOT EXISTS (SELECT * FROM Kategorie)
            BEGIN
                INSERT INTO Kategorie (Nazwa, Typ) VALUES 
                ('Warzywa', 'Świeże'), ('Nabiał', 'Chłodnia'), 
                ('Mięsa', 'Chłodnia'), ('Sosy i Oliwy', 'Suche'), ('Napoje', 'Napój');
            END

            IF NOT EXISTS (SELECT * FROM Dostawcy)
            BEGIN
                INSERT INTO Dostawcy (Nazwa, Telefon, Email, Miasto, Kraj) VALUES 
                ('Hurtownia Ital-Food', '123-456-789', 'zamowienia@italfood.pl', 'Wrocław', 'Polska'),
                ('Lokalny Rolnik - Grzegorz', '555-222-111', 'eko-warzywa@poczta.pl', 'Pszczyna', 'Polska');
            END

            IF NOT EXISTS (SELECT * FROM Magazyn)
            BEGIN
                INSERT INTO Magazyn (Nazwa, Ilosc, Jednostka, Dostepny, ID_Kategorii) VALUES 
                ('Ser Mozzarella', 20.5, 'kg', 1, (SELECT TOP 1 ID_Kategorii FROM Kategorie WHERE Nazwa='Nabiał')),
                ('Mąka Typ 00', 50.0, 'kg', 1, (SELECT TOP 1 ID_Kategorii FROM Kategorie WHERE Nazwa='Sosy i Oliwy')),
                ('Salami Picante', 5.0, 'kg', 1, (SELECT TOP 1 ID_Kategorii FROM Kategorie WHERE Nazwa='Mięsa'));
            END";

            ExecuteQuery(connection, DanePrzyklad);
            Console.WriteLine("Pomyślnie utworzono bazę");
            Console.WriteLine("Czytanie informacji z bazy");
            string ZKategorii = "SELECT Nazwa, Ilosc, Jednostka FROM Magazyn";

                SqlCommand polecenie1 = new SqlCommand(ZKategorii, connection);

                using (SqlDataReader reader = polecenie1.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string nazwa = reader["Nazwa"].ToString();
                        decimal ilosc =(decimal)reader["Ilosc"];
                        string jednostka = reader["Jednostka"].ToString();
                        Console.WriteLine("{0,-20} | {1,-10} | {2,-10}", nazwa, ilosc, jednostka);
                    }
                Console.WriteLine("Koniec informacji z Kategorii");
            }
                ExecuteQuery(connection, ZKategorii);
            string ZDostawcy = "SELECT Nazwa, Telefon, Miasto FROM Dostawcy";

            SqlCommand polecenie2 = new SqlCommand(ZDostawcy, connection);

            using (SqlDataReader reader = polecenie2.ExecuteReader())
            {
                while (reader.Read())
                {
                    string nazwa = reader["Nazwa"].ToString();
                    string telefon = reader["Telefon"] != DBNull.Value ? reader["Telefon"].ToString() : "Brak";
                    string miasto = reader["Miasto"].ToString();
                    Console.WriteLine("{0,-25} | {1,-15} | {2,-20}", nazwa, telefon, miasto);
                }
                Console.WriteLine("Koniec informacji z Dostawcy");
            }
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