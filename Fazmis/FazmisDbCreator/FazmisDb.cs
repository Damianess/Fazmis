using Microsoft.Data.SqlClient;
using System;
using System.Data;

namespace FazmisDbCreator
{
    public class FazmisDb
    {
        private readonly string _connectionString;

        public FazmisDb(string connectionString)
        {
            _connectionString = connectionString;
        }

        public void InitializeDatabase()
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(_connectionString);

            string targetDatabase = !string.IsNullOrEmpty(builder.InitialCatalog)
                                    ? builder.InitialCatalog
                                    : "fazmis";

            builder.InitialCatalog = "master";
            string masterConnString = builder.ConnectionString;

            using (SqlConnection masterConn = new SqlConnection(masterConnString))
            {
                masterConn.Open();
                string createDbSql = $"IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = '{targetDatabase}') CREATE DATABASE [{targetDatabase}]";
                using SqlCommand cmd = new SqlCommand(createDbSql, masterConn);
                cmd.ExecuteNonQuery();
            }

            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                string tablesSql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
                    CREATE TABLE Categories (ID_Category INT PRIMARY KEY IDENTITY(1,1), Name VARCHAR(100) NOT NULL, Type VARCHAR(50) NOT NULL);

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
                    CREATE TABLE Suppliers (ID_Supplier INT PRIMARY KEY IDENTITY(1,1), Name VARCHAR(255) NOT NULL, Phone VARCHAR(20), City VARCHAR(100));

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Warehouse')
                    CREATE TABLE Warehouse (
                        ID_Item INT PRIMARY KEY IDENTITY(1,1), 
                        Name VARCHAR(255) NOT NULL, 
                        Quantity DECIMAL(10,4) NOT NULL, 
                        Unit VARCHAR(50), 
                        MinStock DECIMAL(10,4) DEFAULT 0,
                        ID_Category INT NOT NULL, 
                        FOREIGN KEY (ID_Category) REFERENCES Categories(ID_Category)
                    );

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders')
                    CREATE TABLE Orders (
                        ID_Order INT PRIMARY KEY IDENTITY(1,1),
                        OrderDate DATETIME DEFAULT GETDATE(),
                        ID_Supplier INT NOT NULL,
                        TotalValue DECIMAL(10,2) DEFAULT 0,
                        Status VARCHAR(50) DEFAULT 'Nowe',
                        FOREIGN KEY (ID_Supplier) REFERENCES Suppliers(ID_Supplier)
                    );

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderDetails')
                    CREATE TABLE OrderDetails (
                        ID_OrderDetail INT PRIMARY KEY IDENTITY(1,1),
                        ID_Order INT NOT NULL,
                        ID_Item INT NOT NULL,
                        Quantity DECIMAL(10,4) NOT NULL,
                        UnitPrice DECIMAL(10,2) NOT NULL,
                        FOREIGN KEY (ID_Order) REFERENCES Orders(ID_Order) ON DELETE CASCADE,
                        FOREIGN KEY (ID_Item) REFERENCES Warehouse(ID_Item)
                    );

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UsedProducts')
                    CREATE TABLE UsedProducts (
                        ID_Usage INT PRIMARY KEY IDENTITY(1,1),
                        ID_Item INT NOT NULL,
                        QuantityUsed DECIMAL(10,4) NOT NULL,
                        UsageDate DATETIME DEFAULT GETDATE(),
                        Reason VARCHAR(255),
                        FOREIGN KEY (ID_Item) REFERENCES Warehouse(ID_Item)
                    );

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Recipes')
                    CREATE TABLE Recipes (ID_Recipe INT PRIMARY KEY IDENTITY(1,1), DishName VARCHAR(255) NOT NULL, Price DECIMAL(10,2));

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RecipeIngredients')
                    CREATE TABLE RecipeIngredients (
                        ID_Ingredient INT PRIMARY KEY IDENTITY(1,1),
                        ID_Recipe INT NOT NULL,
                        ID_Item INT NOT NULL,
                        RequiredQuantity DECIMAL(10,4) NOT NULL,
                        FOREIGN KEY (ID_Recipe) REFERENCES Recipes(ID_Recipe) ON DELETE CASCADE,
                        FOREIGN KEY (ID_Item) REFERENCES Warehouse(ID_Item)
                    );";

                using SqlCommand cmd = new SqlCommand(tablesSql, connection);
                cmd.ExecuteNonQuery();
                string seedQuery = @"
                IF NOT EXISTS (SELECT * FROM Categories)
                    INSERT INTO Categories (Name, Type) VALUES ('Składniki', 'Food'), ('Napoje', 'Drink');
                
                IF NOT EXISTS (SELECT * FROM Suppliers)
                    INSERT INTO Suppliers (Name, Phone, City) VALUES ('Hurtownia Ital-Pol', '123456789', 'Warszawa'), ('Mleczarnia Serowa', '987654321', 'Kraków');
                
                IF NOT EXISTS (SELECT * FROM Warehouse)
                    INSERT INTO Warehouse (Name, Quantity, Unit, ID_Category) VALUES ('Mąka Typ 00', 50.5, 'kg', 1), ('Ser Mozzarella', 20.0, 'kg', 1);";

                using SqlCommand seedCmd = new SqlCommand(seedQuery, connection);
                seedCmd.ExecuteNonQuery();
            }
        }

        public DataTable GetDataTable(string query)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            DataTable dt = new DataTable();
            SqlDataAdapter adapter = new SqlDataAdapter(query, connection);
            adapter.Fill(dt);
            return dt;
        }

        public void ExecuteNonQuery(string query, SqlParameter[] parameters = null)
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            connection.Open();
            using SqlCommand cmd = new SqlCommand(query, connection);
            if (parameters != null) cmd.Parameters.AddRange(parameters);
            cmd.ExecuteNonQuery();
        }
    }
}