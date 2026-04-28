using System;
using System.Collections.Generic;

namespace Fazmis
{
    public class Category
    {
        public string Name { get; set; }
        public string Type { get; set; }
    }
    public class Supplier
    {
        public string Name { get; set; }
        public string Phone { get; set; }
        public string City { get; set; }
    }
    public class Product
    {
        public string Name { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal MinStock { get; set; }
        public int CategoryID { get; set; }
    }

    public class OrderItem
    {
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class Order
    {
        public string OrderDate { get; set; }
        public string SupplierName { get; set; }
        public string Status { get; set; }
        public decimal TotalValue { get; set; }
        public List<OrderItem> Items { get; set; } = new();
    }

    public class Ingredient
    {
        public string ItemName { get; set; }
        public decimal RequiredQuantity { get; set; }
    }

    public class Recipe
    {
        public string DishName { get; set; }
        public decimal Price { get; set; }
        public List<Ingredient> Ingredients { get; set; } = new();
    }

    public class Usage
    {
        public string ItemName { get; set; }
        public decimal QuantityUsed { get; set; }
        public DateTime UsageDate { get; set; }
        public string Reason { get; set; }
    }

    public class FullSystemBackup
    {
        public List<Category> Categories { get; set; } = new();
        public List<Supplier> Suppliers { get; set; } = new();
        public List<Product> Products { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
        public List<Usage> Usages { get; set; } = new();
        public List<Recipe> Recipes { get; set; } = new();
    }
}