using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace Fazmis
{
    public class DataExchangeManager
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public void ExportToJson<T>(string filePath, T data)
        {
            string jsonString = JsonSerializer.Serialize(data, _jsonOptions);
            File.WriteAllText(filePath, jsonString);
        }

        public T ImportFromJson<T>(string filePath)
        {
            string jsonString = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(jsonString);
        }
        public FullSystemBackup CreateBackup(dynamic db)
        {
            var backup = new FullSystemBackup();
            var dtCats = db.GetDataTable("SELECT Name, Type FROM Categories");
            foreach (DataRow row in dtCats.Rows)
            {
                backup.Categories.Add(new Category
                {
                    Name = row["Name"].ToString(),
                    Type = row["Type"]?.ToString() ?? ""
                });
            }
            var dtSups = db.GetDataTable("SELECT Name, Phone, City FROM Suppliers");
            foreach (DataRow row in dtSups.Rows)
            {
                backup.Suppliers.Add(new Supplier
                {
                    Name = row["Name"].ToString(),
                    Phone = row["Phone"]?.ToString() ?? "",
                    City = row["City"]?.ToString() ?? ""
                });
            }
            var dtProds = db.GetDataTable("SELECT Name, Quantity, Unit, MinStock, ID_Category FROM Warehouse");
            foreach (DataRow row in dtProds.Rows)
            {
                backup.Products.Add(new Product
                {
                    Name = row["Name"].ToString(),
                    Quantity = Convert.ToDecimal(row["Quantity"]),
                    Unit = row["Unit"].ToString(),
                    MinStock = Convert.ToDecimal(row["MinStock"]),
                    CategoryID = Convert.ToInt32(row["ID_Category"])
                });
            }
            var dtRecs = db.GetDataTable("SELECT ID_Recipe, DishName, Price FROM Recipes");
            foreach (DataRow row in dtRecs.Rows)
            {
                var recipe = new Recipe
                {
                    DishName = row["DishName"].ToString(),
                    Price = row["Price"] != DBNull.Value ? Convert.ToDecimal(row["Price"]) : 0
                };

                int recId = Convert.ToInt32(row["ID_Recipe"]);
                var dtIngs = db.GetDataTable($@"
                    SELECT w.Name, ri.RequiredQuantity 
                    FROM RecipeIngredients ri
                    JOIN Warehouse w ON ri.ID_Item = w.ID_Item
                    WHERE ri.ID_Recipe = {recId}");

                foreach (DataRow ingRow in dtIngs.Rows)
                {
                    recipe.Ingredients.Add(new Ingredient
                    {
                        ItemName = ingRow["Name"].ToString(),
                        RequiredQuantity = Convert.ToDecimal(ingRow["RequiredQuantity"])
                    });
                }
                backup.Recipes.Add(recipe);
            }
            var dtOrders = db.GetDataTable(@"
                SELECT o.ID_Order, o.OrderDate, s.Name as SupplierName, o.Status, o.TotalValue 
                FROM Orders o
                LEFT JOIN Suppliers s ON o.ID_Supplier = s.ID_Supplier");

            foreach (DataRow row in dtOrders.Rows)
            {
                var order = new Order
                {
                    OrderDate = row["OrderDate"].ToString(),
                    SupplierName = row["SupplierName"]?.ToString() ?? "Brak dostawcy",
                    Status = row["Status"]?.ToString() ?? "",
                    TotalValue = Convert.ToDecimal(row["TotalValue"])
                };

                int ordId = Convert.ToInt32(row["ID_Order"]);
                var dtItems = db.GetDataTable($@"
                    SELECT w.Name, od.Quantity, od.UnitPrice 
                    FROM OrderDetails od
                    JOIN Warehouse w ON od.ID_Item = w.ID_Item
                    WHERE od.ID_Order = {ordId}");

                foreach (DataRow itemRow in dtItems.Rows)
                {
                    order.Items.Add(new OrderItem
                    {
                        ProductName = itemRow["Name"].ToString(),
                        Quantity = Convert.ToDecimal(itemRow["Quantity"]),
                        UnitPrice = Convert.ToDecimal(itemRow["UnitPrice"])
                    });
                }
                backup.Orders.Add(order);
            }
            var dtUsages = db.GetDataTable(@"
                SELECT w.Name, u.QuantityUsed, u.UsageDate, u.Reason 
                FROM UsedProducts u
                JOIN Warehouse w ON u.ID_Item = w.ID_Item");

            foreach (DataRow row in dtUsages.Rows)
            {
                backup.Usages.Add(new Usage
                {
                    ItemName = row["Name"].ToString(),
                    QuantityUsed = Convert.ToDecimal(row["QuantityUsed"]),
                    UsageDate = Convert.ToDateTime(row["UsageDate"]),
                    Reason = row["Reason"]?.ToString() ?? ""
                });
            }

            return backup;
        }
    }
}