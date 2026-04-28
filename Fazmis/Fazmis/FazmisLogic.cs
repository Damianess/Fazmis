using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using FazmisDbCreator;

namespace Fazmis
{
    public class FazmisLogic
    {
        private readonly FazmisDb _db;

        public FazmisLogic(FazmisDb db) => _db = db;
        public DataTable GetWarehouseWithCategoryNames()
        {
            string query = @"
        SELECT W.ID_Item,  W.Name, W.Quantity,  W.Unit, W.MinStock, C.Name AS CategoryName FROM Warehouse W JOIN Categories C ON W.ID_Category = C.ID_Category";
            return _db.GetDataTable(query);
        }
        public bool ProductExists(int itemId)
        {
            DataTable dt = _db.GetDataTable($"SELECT COUNT(*) FROM Warehouse WHERE ID_Item = {itemId}");
            return Convert.ToInt32(dt.Rows[0][0]) > 0;
        }
        public bool IsItemUsed(int itemId)
        {
            string query = $@"
        SELECT 
        (SELECT COUNT(*) FROM OrderDetails WHERE ID_Item = {itemId}) +
        (SELECT COUNT(*) FROM RecipeIngredients WHERE ID_Item = {itemId}) +
        (SELECT COUNT(*) FROM UsedProducts WHERE ID_Item = {itemId})";

            DataTable dt = _db.GetDataTable(query);
            return Convert.ToInt32(dt.Rows[0][0]) > 0;
        }
        public int GetOrCreateCategoryId(string categoryName)
        {
            DataTable dt = _db.GetDataTable($"SELECT ID_Category FROM Categories WHERE Name = '{categoryName}'");

            if (dt.Rows.Count > 0)
            {
                return (int)dt.Rows[0]["ID_Category"];
            }
            else
            {
                _db.ExecuteNonQuery("INSERT INTO Categories (Name, Type) VALUES (@n, 'Jedzenie')",
                    new[] { new SqlParameter("@n", categoryName) });

                DataTable dtNew = _db.GetDataTable($"SELECT ID_Category FROM Categories WHERE Name = '{categoryName}'");
                return (int)dtNew.Rows[0]["ID_Category"];
            }
        }
        public void AddCategory(string name, string type) =>
            _db.ExecuteNonQuery("INSERT INTO Categories (Name, Type) VALUES (@n, @t)",
                new[] { new SqlParameter("@n", name), new SqlParameter("@t", type) });

        public void DeleteCategory(int id) =>
            _db.ExecuteNonQuery("DELETE FROM Categories WHERE ID_Category = @id",
                new[] { new SqlParameter("@id", id) });


        public void AddSupplier(string name, string phone, string city) =>
            _db.ExecuteNonQuery("INSERT INTO Suppliers (Name, Phone, City) VALUES (@n, @p, @c)",
                new[] { new SqlParameter("@n", name), new SqlParameter("@p", phone), new SqlParameter("@c", city) });

        public void UpdateSupplier(int id, string name, string phone, string city) =>
            _db.ExecuteNonQuery("UPDATE Suppliers SET Name=@n, Phone=@p, City=@c WHERE ID_Supplier=@id",
                new[] { new SqlParameter("@n", name), new SqlParameter("@p", phone), new SqlParameter("@c", city), new SqlParameter("@id", id) });

        public void DeleteSupplier(int id) =>
            _db.ExecuteNonQuery("DELETE FROM Suppliers WHERE ID_Supplier = @id", new[] { new SqlParameter("@id", id) });


        public void AddProduct(string name, decimal qty, string unit, decimal minStock, int categoryId) =>
            _db.ExecuteNonQuery("INSERT INTO Warehouse (Name, Quantity, Unit, MinStock, ID_Category) VALUES (@n, @q, @u, @m, @c)",
                new[] { new SqlParameter("@n", name), new SqlParameter("@q", qty), new SqlParameter("@u", unit), new SqlParameter("@m", minStock), new SqlParameter("@c", categoryId) });
        public void UpdateProduct(int id, string name, decimal qty, string unit, decimal min, int catId)
        {
            string query = @"UPDATE Warehouse 
                     SET Name = @n, Quantity = @q, Unit = @u, MinStock = @m, ID_Category = @c 
                     WHERE ID_Item = @id";

            _db.ExecuteNonQuery(query, new[] {
        new SqlParameter("@n", name),
        new SqlParameter("@q", qty),
        new SqlParameter("@u", unit),
        new SqlParameter("@m", min),
        new SqlParameter("@c", catId),
        new SqlParameter("@id", id)
    });
        }

        public void AdjustStock(int itemId, decimal difference) =>
            _db.ExecuteNonQuery("UPDATE Warehouse SET Quantity = Quantity + @diff WHERE ID_Item = @id",
                new[] { new SqlParameter("@diff", difference), new SqlParameter("@id", itemId) });

        public void DeleteProduct(int id) =>
            _db.ExecuteNonQuery("DELETE FROM Warehouse WHERE ID_Item = @id", new[] { new SqlParameter("@id", id) });

        public void CreateOrder(int supplierId, decimal total, string status = "Nowe") =>
            _db.ExecuteNonQuery("INSERT INTO Orders (ID_Supplier, TotalValue, Status) VALUES (@s, @t, @st)",
                new[] { new SqlParameter("@s", supplierId), new SqlParameter("@t", total), new SqlParameter("@st", status) });

        public void UpdateOrderStatus(int orderId, string newStatus) =>
            _db.ExecuteNonQuery("UPDATE Orders SET Status = @st WHERE ID_Order = @id",
                new[] { new SqlParameter("@st", newStatus), new SqlParameter("@id", orderId) });
        public void AddRecipe(string dishName, decimal price) =>
            _db.ExecuteNonQuery("INSERT INTO Recipes (DishName, Price) VALUES (@n, @p)",
                new[] { new SqlParameter("@n", dishName), new SqlParameter("@p", price) });

        public void DeleteRecipe(int id) =>
            _db.ExecuteNonQuery("DELETE FROM Recipes WHERE ID_Recipe = @id", new[] { new SqlParameter("@id", id) });

        public void LogUsage(int itemId, decimal qty, string reason)
        {
            _db.ExecuteNonQuery("INSERT INTO UsedProducts (ID_Item, QuantityUsed, Reason) VALUES (@id, @q, @r)",
                new[] { new SqlParameter("@id", itemId), new SqlParameter("@q", qty), new SqlParameter("@r", reason) });
            AdjustStock(itemId, -qty);
        }

        public DataTable GetAllFromTable(string tableName) => _db.GetDataTable($"SELECT * FROM {tableName}");
    }
}