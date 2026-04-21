using System.Data;
using Microsoft.Data.SqlClient;
using FazmisDbCreator;

namespace Fazmis
{
    public class FazmisLogic
    {
        private readonly FazmisDb _db;

        public FazmisLogic(string connectionString)
        {
            _db = new FazmisDb(connectionString);
        }

        public void SetupApp() => _db.InitializeDatabase();

        public DataTable GetWarehouseData() => _db.GetDataTable("SELECT Name, Quantity, Unit FROM Warehouse");

        public DataTable GetSuppliersData() => _db.GetDataTable("SELECT Name, Phone, City FROM Suppliers");

        public void DeleteSupplier(string name)
        {
            string query = "DELETE FROM Suppliers WHERE Name = @name";
            SqlParameter[] paras = { new SqlParameter("@name", name) };
            _db.ExecuteNonQuery(query, paras);
        }
    }
}