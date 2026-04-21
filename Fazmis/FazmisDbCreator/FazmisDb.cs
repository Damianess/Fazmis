using Microsoft.Data.SqlClient;
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
            using SqlConnection connection = new SqlConnection(_connectionString);
            connection.Open();

            string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Categories')
                CREATE TABLE Categories (ID_Category INT PRIMARY KEY IDENTITY(1,1), Name VARCHAR(100) NOT NULL, Type VARCHAR(50) NOT NULL);

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
                CREATE TABLE Suppliers (ID_Supplier INT PRIMARY KEY IDENTITY(1,1), Name VARCHAR(255) NOT NULL, Phone VARCHAR(20), City VARCHAR(100));

                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Warehouse')
                CREATE TABLE Warehouse (ID_Item INT PRIMARY KEY IDENTITY(1,1), Name VARCHAR(255) NOT NULL, Quantity DECIMAL(10,4) NOT NULL, Unit VARCHAR(50), ID_Category INT NOT NULL, FOREIGN KEY (ID_Category) REFERENCES Categories(ID_Category));";

            using SqlCommand cmd = new SqlCommand(sql, connection);
            cmd.ExecuteNonQuery();
            string seedQuery = @"
                IF NOT EXISTS (SELECT * FROM Categories)
                INSERT INTO Categories (Name, Type) VALUES ('Składniki', 'Food'), ('Napoje', 'Drink');

                IF NOT EXISTS (SELECT * FROM Suppliers)
                INSERT INTO Suppliers (Name, Phone, City) VALUES ('Hurtownia Ital-Pol', '123456789', 'Warszawa'), ('Mleczarnia Serowa', '987654321', 'Kraków');
        
                IF NOT EXISTS (SELECT * FROM Warehouse)
                INSERT INTO Warehouse (Name, Quantity, Unit, ID_Category) VALUES ('Mąka Typ 00', 50.5, 'kg', 1), ('Ser Mozzarella', 20.0, 'kg', 1);
        ";
            using SqlCommand seedCmd = new SqlCommand(seedQuery, connection);
            seedCmd.ExecuteNonQuery();
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