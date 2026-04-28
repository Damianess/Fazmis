using FazmisDbCreator; 
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using System;
using System.Data;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace Fazmis
{
    public partial class MainWindow : Window
    {
        private FazmisDb _db;
        private FazmisLogic _logic;
        private DataExchangeManager _dataManager;

        public MainWindow()
        {
            InitializeComponent();
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            string connString = config.GetConnectionString("FazmisDb");
            _db = new FazmisDb(connString);
            _db.InitializeDatabase();
            try
            {
                DataTable dt = _db.GetDataTable("SELECT physical_name FROM sys.database_files");
                string paths = "";
                foreach (DataRow row in dt.Rows)
                {
                    paths += row["physical_name"].ToString() + "\n";
                }
                MessageBox.Show("Fizyczne pliki bazy danych:\n" + paths);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Nie udało się pobrać ścieżki: " + ex.Message);
            }

            _logic = new FazmisLogic(_db);
            _dataManager = new DataExchangeManager();

            RefreshAllGrids();
        }

        private void RefreshAllGrids()
        {
            try
            {
                DgWarehouse.ItemsSource = _logic.GetWarehouseWithCategoryNames().DefaultView;
                DgSuppliers.ItemsSource = _logic.GetAllFromTable("Suppliers").DefaultView;
                DgOrders.ItemsSource = _logic.GetAllFromTable("Orders").DefaultView;
                DgCategories.ItemsSource = _logic.GetAllFromTable("Categories").DefaultView;
                DgUsedProducts.ItemsSource = _logic.GetAllFromTable("UsedProducts").DefaultView;
                DgRecipes.ItemsSource = _logic.GetAllFromTable("Recipes").DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd podczas odświeżania danych: " + ex.Message);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => RefreshAllGrids();
        private void DgOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgOrders.SelectedItem is DataRowView row)
            {
                string orderId = row["ID_Order"].ToString();
                TxtOrdSupId.Text = row["ID_Supplier"].ToString();
                TxtOrdStatus.Text = row["Status"]?.ToString();
                TxtDetOrderId.Text = orderId;
                LoadOrderDetails(int.Parse(orderId));
            }
        }
        private void LoadOrderDetails(int orderId)
        {
            string query = $@"SELECT OD.ID_OrderDetail, OD.ID_Item, W.Name as Produkt, 
                             OD.Quantity, OD.UnitPrice, 
                             (OD.Quantity * OD.UnitPrice) as Suma 
                      FROM OrderDetails OD 
                      JOIN Warehouse W ON OD.ID_Item = W.ID_Item 
                      WHERE OD.ID_Order = {orderId}";
            DgOrderDetails.ItemsSource = _db.GetDataTable(query).DefaultView;
        }

        private void BtnExportAll_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog { Filter = "JSON File (*.json)|*.json" };
            if (sfd.ShowDialog() == true)
            {
                try
                {
                    var backupData = _dataManager.CreateBackup(_db);
                    _dataManager.ExportToJson(sfd.FileName, backupData);

                    MessageBox.Show("Eksport wszystkich danych zakończony pomyślnie!", "Sukces");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas eksportu: {ex.Message}", "Błąd krytyczny");
                }
            }
        }

        private void BtnImportAll_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "JSON File (*.json)|*.json" };
            if (ofd.ShowDialog() == true)
            {
                try
                {
                    var backup = _dataManager.ImportFromJson<FullSystemBackup>(ofd.FileName);
                    if (backup == null) return;
                    if (backup.Categories != null)
                    {
                        foreach (var cat in backup.Categories)
                        {
                            string check = $"SELECT COUNT(*) FROM Categories WHERE Name = '{cat.Name.Replace("'", "''")}'";
                            if (Convert.ToInt32(_db.GetDataTable(check).Rows[0][0]) == 0)
                            {
                                _db.ExecuteNonQuery($"INSERT INTO Categories (Name, Type) VALUES ('{cat.Name.Replace("'", "''")}', '{cat.Type.Replace("'", "''")}')");
                            }
                        }
                    }
                    if (backup.Suppliers != null)
                    {
                        foreach (var sup in backup.Suppliers)
                        {
                            string check = $"SELECT COUNT(*) FROM Suppliers WHERE Name = '{sup.Name.Replace("'", "''")}'";
                            if (Convert.ToInt32(_db.GetDataTable(check).Rows[0][0]) == 0)
                            {
                                _db.ExecuteNonQuery($"INSERT INTO Suppliers (Name, Phone, City) VALUES ('{sup.Name.Replace("'", "''")}', '{sup.Phone}', '{sup.City.Replace("'", "''")}')");
                            }
                        }
                    }
                    if (backup.Products != null)
                    {
                        foreach (var prod in backup.Products)
                        {
                            string check = $"SELECT COUNT(*) FROM Warehouse WHERE Name = '{prod.Name.Replace("'", "''")}'";
                            if (Convert.ToInt32(_db.GetDataTable(check).Rows[0][0]) == 0)
                            {
                                _logic.AddProduct(prod.Name, prod.Quantity, prod.Unit, prod.MinStock, prod.CategoryID);
                            }
                        }
                    }
                    if (backup.Recipes != null)
                    {
                        foreach (var rec in backup.Recipes)
                        {
                            string safeDish = rec.DishName.Replace("'", "''");
                            string checkRec = $"SELECT COUNT(*) FROM Recipes WHERE DishName = '{safeDish}'";

                            if (Convert.ToInt32(_db.GetDataTable(checkRec).Rows[0][0]) == 0)
                            {
                                string price = rec.Price.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                _db.ExecuteNonQuery($"INSERT INTO Recipes (DishName, Price) VALUES ('{safeDish}', {price})");
                            }
                            var dtRec = _db.GetDataTable($"SELECT ID_Recipe FROM Recipes WHERE DishName = '{safeDish}'");
                            if (dtRec.Rows.Count > 0 && rec.Ingredients != null)
                            {
                                int recipeId = Convert.ToInt32(dtRec.Rows[0]["ID_Recipe"]);

                                foreach (var ing in rec.Ingredients)
                                {
                                    string safeIngName = ing.ItemName.Replace("'", "''");
                                    var dtItem = _db.GetDataTable($"SELECT * FROM Warehouse WHERE Name = '{safeIngName}'");

                                    if (dtItem.Rows.Count > 0)
                                    {
                                        int itemId = Convert.ToInt32(dtItem.Rows[0][0]); 
                                        string qty = ing.RequiredQuantity.ToString(System.Globalization.CultureInfo.InvariantCulture);

                                        _db.ExecuteNonQuery($@"
                        INSERT INTO RecipeIngredients (ID_Recipe, ID_Item, RequiredQuantity) 
                        VALUES ({recipeId}, {itemId}, {qty})");
                                    }
                                }
                            }
                        }
                    }
                    if (backup.Orders != null)
                    {
                        foreach (var ord in backup.Orders)
                        {
                            string safeSupName = ord.SupplierName.Replace("'", "''");
                            var dtSup = _db.GetDataTable($"SELECT * FROM Suppliers WHERE Name = '{safeSupName}'");

                            if (dtSup.Rows.Count > 0)
                            {
                                int idSupplier = Convert.ToInt32(dtSup.Rows[0][0]);

                                string safeStatus = ord.Status.Replace("'", "''");
                                string totalValue = ord.TotalValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                _db.ExecuteNonQuery($@"INSERT INTO Orders (OrderDate, ID_Supplier, Status, TotalValue) 
                                  VALUES ('{ord.OrderDate}', {idSupplier}, '{safeStatus}', {totalValue})");
                                var dtOrd = _db.GetDataTable($"SELECT * FROM Orders WHERE OrderDate = '{ord.OrderDate}' AND ID_Supplier = {idSupplier} ORDER BY 1 DESC");

                                if (dtOrd.Rows.Count > 0 && ord.Items != null)
                                {
                                    int orderId = Convert.ToInt32(dtOrd.Rows[0][0]);

                                    foreach (var item in ord.Items)
                                    {
                                        string safeProdName = item.ProductName.Replace("'", "''");
                                        var dtItem = _db.GetDataTable($"SELECT * FROM Warehouse WHERE Name = '{safeProdName}'");

                                        if (dtItem.Rows.Count > 0)
                                        {
                                            int itemId = Convert.ToInt32(dtItem.Rows[0][0]);
                                            string q = item.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                            string p = item.UnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture);

                                            _db.ExecuteNonQuery($@"
                            INSERT INTO OrderDetails (ID_Order, ID_Item, Quantity, UnitPrice) 
                            VALUES ({orderId}, {itemId}, {q}, {p})");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (backup.Usages != null)
                    {
                        foreach (var usage in backup.Usages)
                        {
                            string safeItemName = usage.ItemName.Replace("'", "''");
                            string getIDQuery = $"SELECT * FROM Warehouse WHERE Name = '{safeItemName}'";
                            var dt = _db.GetDataTable(getIDQuery);

                            if (dt.Rows.Count > 0)
                            {
                                int idItem = Convert.ToInt32(dt.Rows[0][0]);

                                string qty = usage.QuantityUsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                string safeReason = usage.Reason?.Replace("'", "''") ?? "";
                                string dateFormatted = usage.UsageDate.ToString("yyyy-MM-dd HH:mm:ss");
                                _db.ExecuteNonQuery($@"
                INSERT INTO UsedProducts (ID_Item, QuantityUsed, UsageDate, Reason) 
                VALUES ({idItem}, {qty}, '{dateFormatted}', '{safeReason}')");
                            }
                        }
                    }
                    RefreshAllGrids();
                    MessageBox.Show("Import wszystkich danych zakończony pomyślnie!", "Sukces");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas pełnego importu: {ex.Message}", "Błąd krytyczny");
                }
            }
        }
        private void BtnSaveProduct_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string prodName = TxtProdName.Text;
                string catName = TxtProdCat.Text;
                string unit = TxtProdUnit.Text;

                if (string.IsNullOrWhiteSpace(prodName) || string.IsNullOrWhiteSpace(catName))
                {
                    MessageBox.Show("Nazwa produktu i kategorii nie mogą być puste!");
                    return;
                }
                if (!decimal.TryParse(TxtProdQty.Text.Replace(".", ","), out decimal qty))
                {
                    MessageBox.Show("Błędna ilość!");
                    return;
                }
                int catId = _logic.GetOrCreateCategoryId(catName);
                _logic.AddProduct(prodName, qty, unit, 0, catId);
                ClearProductFields();
                RefreshAllGrids();
                MessageBox.Show($"Dodano produkt do kategorii '{catName}'");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Błąd: " + ex.Message);
            }
        }
        private void DgWarehouse_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgWarehouse.SelectedItem is DataRowView row)
            {
                TxtProdName.Text = row["Name"].ToString();
                TxtProdQty.Text = row["Quantity"].ToString();
                TxtProdUnit.Text = row["Unit"].ToString();
                TxtProdMin.Text = row["MinStock"].ToString();
                TxtProdCat.Text = row["CategoryName"].ToString();
            }
        }
        private void ClearProductFields()
        {
            TxtProdName.Clear();
            TxtProdQty.Clear();
            TxtProdUnit.Clear();
            TxtProdMin.Clear();
            TxtProdCat.Clear();
        }
        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9,.]+");
            e.Handled = regex.IsMatch(e.Text);
        }
        private bool IsPhoneNumberValid(string phone)
        {
            return string.IsNullOrEmpty(phone) || Regex.IsMatch(phone, @"^[0-9+ ]+$");
        }
        private void BtnDeleteProduct_Click(object sender, RoutedEventArgs e)
        {
            if (DgWarehouse.SelectedItem is DataRowView row)
            {
                int itemId = Convert.ToInt32(row["ID_Item"]);
                if (_logic.IsItemUsed(itemId))
                {
                    MessageBox.Show("Nie można usunąć tego produktu! \nJest on powiązany z zamówieniami, przepisami lub historią zużycia.",
                                    "Błąd usuwania", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (MessageBox.Show("Czy na pewno chcesz usunąć ten produkt z magazynu?", "Potwierdzenie", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        _db.ExecuteNonQuery($"DELETE FROM Warehouse WHERE ID_Item = {itemId}");
                        RefreshAllGrids();
                        MessageBox.Show("Produkt został usunięty.");
                    }
                    catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
                }
            }
        }
        private void BtnUpdateProduct_Click(object sender, RoutedEventArgs e)
        {
            if (DgWarehouse.SelectedItem is DataRowView row)
            {
                try
                {
                    int id = (int)row["ID_Item"];
                    string name = TxtProdName.Text;
                    decimal qty = decimal.Parse(TxtProdQty.Text.Replace(".", ","));
                    string unit = TxtProdUnit.Text;
                    decimal min = decimal.Parse(TxtProdMin.Text.Replace(".", ","));
                    int catId = _logic.GetOrCreateCategoryId(TxtProdCat.Text);
                    _logic.UpdateProduct(id, name, qty, unit, min, catId);

                    RefreshAllGrids();
                    MessageBox.Show("Produkt zaktualizowany!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd podczas aktualizacji: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Najpierw zaznacz produkt w tabeli!");
            }
        }
        private void BtnSaveSupplier_Click(object sender, RoutedEventArgs e)
        {
            string phone = TxtSupPhone.Text.Trim();
            if (!IsPhoneNumberValid(phone))
            {
                MessageBox.Show("Numer telefonu może zawierać tylko cyfry!", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtSupName.Text))
            {
                MessageBox.Show("Nazwa dostawcy jest wymagana!", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string name = TxtSupName.Text.Trim().Replace("'", "''");
                string city = TxtSupCity.Text.Trim().Replace("'", "''");

                string query = $"INSERT INTO Suppliers (Name, Phone, City) VALUES ('{name}', '{phone}', '{city}')";

                _db.ExecuteNonQuery(query);
                RefreshAllGrids();
                ClearSupplierInputs(); 
                MessageBox.Show("Dostawca został dodany.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania: {ex.Message}");
            }
        }

        private void BtnUpdateSupplier_Click(object sender, RoutedEventArgs e)
        {
            string phone = TxtSupPhone.Text.Trim();
            if (!IsPhoneNumberValid(phone))
            {
                MessageBox.Show("Numer telefonu może zawierać tylko cyfry!", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (DgSuppliers.SelectedItem is DataRowView row)
            {
                if (string.IsNullOrWhiteSpace(TxtSupName.Text))
                {
                    MessageBox.Show("Nazwa dostawcy nie może być pusta!");
                    return;
                }

                try
                {
                    int id = Convert.ToInt32(row["ID_Supplier"]);
                    string name = TxtSupName.Text.Trim().Replace("'", "''");
                    string city = TxtSupCity.Text.Trim().Replace("'", "''");

                    string query = $"UPDATE Suppliers SET Name='{name}', Phone='{phone}', City='{city}' WHERE ID_Supplier={id}";

                    _db.ExecuteNonQuery(query);
                    RefreshAllGrids();
                    MessageBox.Show("Dane dostawcy zostały zaktualizowane.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas aktualizacji: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Wybierz dostawcę z listy do edycji.");
            }
        }

        private void BtnDeleteSupplier_Click(object sender, RoutedEventArgs e)
        {
            if (DgSuppliers.SelectedItem is DataRowView row)
            {
                var result = MessageBox.Show("Czy na pewno chcesz usunąć tego dostawcę?", "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        int id = Convert.ToInt32(row["ID_Supplier"]);
                        _db.ExecuteNonQuery($"DELETE FROM Suppliers WHERE ID_Supplier={id}");
                        RefreshAllGrids();
                        ClearSupplierInputs();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Nie można usunąć dostawcy, który posiada przypisane zamówienia w systemie.");
                    }
                }
            }
        }
        private void ClearSupplierInputs()
        {
            TxtSupName.Clear();
            TxtSupPhone.Clear();
            TxtSupCity.Clear();
        }

        private void DgSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgSuppliers.SelectedItem is DataRowView row)
            {
                TxtSupName.Text = row["Name"]?.ToString();
                TxtSupPhone.Text = row["Phone"]?.ToString();
                TxtSupCity.Text = row["City"]?.ToString();
            }
        }
        private void BtnSaveCategory_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtCatName.Text) || string.IsNullOrWhiteSpace(TxtCatType.Text))
            {
                MessageBox.Show("Pola 'Nazwa' i 'Typ' nie mogą być puste!", "Błąd walidacji", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                string name = TxtCatName.Text.Trim().Replace("'", "''");
                string type = TxtCatType.Text.Trim().Replace("'", "''");

                string query = $"INSERT INTO Categories (Name, Type) VALUES ('{name}', '{type}')";

                _db.ExecuteNonQuery(query);
                RefreshAllGrids();
                ClearCategoryInputs();
                MessageBox.Show("Nowa kategoria została dodana.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas dodawania kategorii: {ex.Message}");
            }
        }

        private void BtnUpdateCategory_Click(object sender, RoutedEventArgs e)
        {
            if (DgCategories.SelectedItem is DataRowView row)
            {
                if (string.IsNullOrWhiteSpace(TxtCatName.Text))
                {
                    MessageBox.Show("Nazwa kategorii jest wymagana!");
                    return;
                }

                try
                {
                    int id = Convert.ToInt32(row["ID_Category"]);
                    string name = TxtCatName.Text.Trim().Replace("'", "''");
                    string type = TxtCatType.Text.Trim().Replace("'", "''");

                    string query = $"UPDATE Categories SET Name='{name}', Type='{type}' WHERE ID_Category={id}";

                    _db.ExecuteNonQuery(query);
                    RefreshAllGrids();
                    MessageBox.Show("Kategoria zaktualizowana.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas aktualizacji: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Wybierz kategorię z listy do edycji.");
            }
        }

        private void BtnDeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (DgCategories.SelectedItem is DataRowView row)
            {
                var result = MessageBox.Show("Czy na pewno chcesz usunąć tę kategorię? Upewnij się, że nie jest przypisana do żadnych produktów.",
                                           "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        int id = Convert.ToInt32(row["ID_Category"]);
                        _db.ExecuteNonQuery($"DELETE FROM Categories WHERE ID_Category={id}");
                        RefreshAllGrids();
                        ClearCategoryInputs();
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Nie można usunąć kategorii, która zawiera produkty. Najpierw usuń produkty lub zmień ich kategorię.",
                                        "Błąd więzów integralności", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void ClearCategoryInputs()
        {
            TxtCatName.Clear();
            TxtCatType.Clear();
        }

        private void DgCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgCategories.SelectedItem is DataRowView row)
            {
                TxtCatName.Text = row["Name"]?.ToString();
                TxtCatType.Text = row["Type"]?.ToString();
            }
        }
        private void BtnSaveRecipe_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtRecipeName.Text))
            {
                MessageBox.Show("Nazwa dania nie może być pusta!");
                return;
            }
            if (!decimal.TryParse(TxtRecipePrice.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal priceValue))
            {
                MessageBox.Show("Wprowadź poprawną cenę (np. 34.50).");
                return;
            }

            try
            {
                string name = TxtRecipeName.Text.Trim().Replace("'", "''");
                string priceFormatted = priceValue.ToString(System.Globalization.CultureInfo.InvariantCulture);

                string query = $"INSERT INTO Recipes (DishName, Price) VALUES ('{name}', {priceFormatted})";

                _db.ExecuteNonQuery(query);
                RefreshAllGrids();

                TxtRecipeName.Clear();
                TxtRecipePrice.Clear();
                MessageBox.Show("Przepis (danie) został zapisany.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd podczas zapisywania przepisu: {ex.Message}");
            }
        }
        private void BtnUpdateRecipe_Click(object sender, RoutedEventArgs e)
        {
            if (DgRecipes.SelectedItem is DataRowView row)
            {
                try
                {
                    int recipeId = Convert.ToInt32(row["ID_Recipe"]);
                    string newName = TxtRecipeName.Text.Trim().Replace("'", "''");
                    if (!decimal.TryParse(TxtRecipePrice.Text.Replace(",", "."),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out decimal priceValue))
                    {
                        MessageBox.Show("Wprowadź poprawną cenę (np. 34.50).");
                        return;
                    }
                    string priceSql = priceValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    string query = $@"UPDATE Recipes 
                              SET DishName = '{newName}', 
                                  Price = {priceSql} 
                              WHERE ID_Recipe = {recipeId}";
                    _db.ExecuteNonQuery(query);
                    RefreshAllGrids();

                    MessageBox.Show($"Danie '{newName}' zostało zaktualizowane.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Błąd podczas aktualizacji przepisu: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Najpierw zaznacz przepis na liście, który chcesz edytować.");
            }
        }
        private void BtnDeleteRecipe_Click(object sender, RoutedEventArgs e)
        {
            if (DgRecipes.SelectedItem is DataRowView row)
            {
                var result = MessageBox.Show($"Czy na pewno chcesz usunąć danie '{row["DishName"]}' wraz ze wszystkimi przypisanymi składnikami?",
                                           "Potwierdzenie", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        int id = Convert.ToInt32(row["ID_Recipe"]);
                        _db.ExecuteNonQuery($"DELETE FROM Recipes WHERE ID_Recipe={id}");
                        RefreshAllGrids();

                        TxtRecipeName.Clear();
                        TxtRecipePrice.Clear();
                        DgRecipeIngredients.ItemsSource = null; 
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Błąd podczas usuwania: {ex.Message}");
                    }
                }
            }
            else
            {
                MessageBox.Show("Najpierw zaznacz przepis na liście.");
            }
        }

        private void DgRecipes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgRecipes.SelectedItem is DataRowView row)
            {
                TxtRecipeName.Text = row["DishName"].ToString();
                TxtRecipePrice.Text = row["Price"].ToString();
                int recipeId = Convert.ToInt32(row["ID_Recipe"]);
                LoadRecipeIngredients(recipeId);
            }
        }

        private void LoadRecipeIngredients(int recipeId)
        {
            string query = $@"
        SELECT RI.ID_Ingredient, RI.ID_Item, W.Name as Składnik, RI.RequiredQuantity as Ilość, W.Unit as Jednostka
        FROM RecipeIngredients RI 
        JOIN Warehouse W ON RI.ID_Item = W.ID_Item 
        WHERE RI.ID_Recipe = {recipeId}";
            DgRecipeIngredients.ItemsSource = _db.GetDataTable(query).DefaultView;
        }
        private void DgRecipeIngredients_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgRecipeIngredients.SelectedItem is DataRowView row)
            {
                TxtIngredId.Text = row["ID_Item"].ToString();
                TxtIngredQty.Text = row["Ilość"].ToString();
            }
        }
        private void BtnAddIngredient_Click(object sender, RoutedEventArgs e)
        {
            if (DgRecipes.SelectedItem is DataRowView selectedRecipe)
            {
                if (string.IsNullOrWhiteSpace(TxtIngredId.Text) || string.IsNullOrWhiteSpace(TxtIngredQty.Text))
                {
                    MessageBox.Show("Podaj ID produktu oraz ilość!");
                    return;
                }

                try
                {
                    int itemId = int.Parse(TxtIngredId.Text);
                    if (!_logic.ProductExists(itemId))
                    {
                        MessageBox.Show($"Nie można dodać składnika. Produkt o ID {itemId} nie istnieje w magazynie!");
                        return;
                    }

                    int recipeId = Convert.ToInt32(selectedRecipe["ID_Recipe"]);
                    string qty = TxtIngredQty.Text.Replace(",", ".");
                    string query = $@"INSERT INTO RecipeIngredients (ID_Recipe, ID_Item, RequiredQuantity) 
                             VALUES ({recipeId}, {itemId}, {qty})";

                    _db.ExecuteNonQuery(query);
                    LoadRecipeIngredients(recipeId);

                    TxtIngredId.Clear();
                    TxtIngredQty.Clear();
                    MessageBox.Show("Składnik został dodany do przepisu.");
                }
                catch (FormatException)
                {
                    MessageBox.Show("ID produktu i ilość muszą być liczbami!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Najpierw zaznacz danie na liście powyżej!");
            }
        }
        private void BtnUpdateIngredient_Click(object sender, RoutedEventArgs e)
        {
            if (DgRecipeIngredients.SelectedItem is DataRowView row && DgRecipes.SelectedItem is DataRowView recipeRow)
            {
                try
                {
                    int ingredientId = Convert.ToInt32(row["ID_Ingredient"]);
                    int recipeId = Convert.ToInt32(recipeRow["ID_Recipe"]);
                    string qty = TxtIngredQty.Text.Replace(",", ".");

                    _db.ExecuteNonQuery($"UPDATE RecipeIngredients SET RequiredQuantity = {qty} WHERE ID_Ingredient = {ingredientId}");

                    LoadRecipeIngredients(recipeId); 
                    MessageBox.Show("Ilość składnika została zaktualizowana.");
                }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
            }
        }

        private void BtnDeleteIngredient_Click(object sender, RoutedEventArgs e)
        {
            if (DgRecipeIngredients.SelectedItem is DataRowView row && DgRecipes.SelectedItem is DataRowView recipeRow)
            {
                try
                {
                    int ingredientId = Convert.ToInt32(row["ID_Ingredient"]);
                    int recipeId = Convert.ToInt32(recipeRow["ID_Recipe"]);

                    _db.ExecuteNonQuery($"DELETE FROM RecipeIngredients WHERE ID_Ingredient = {ingredientId}");

                    LoadRecipeIngredients(recipeId);
                    MessageBox.Show("Składnik usunięty z przepisu.");
                }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
            }
        }
        private void BtnAddUsage_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtUsedProdId.Text) || string.IsNullOrWhiteSpace(TxtUsedQty.Text))
            {
                MessageBox.Show("Podaj ID produktu oraz ilość zużycia!", "Błąd danych", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(TxtUsedQty.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out decimal qtyValue))
            {
                MessageBox.Show("Nieprawidłowy format ilości. Użyj cyfr.");
                return;
            }
            if (!int.TryParse(TxtUsedProdId.Text, out int itemId))
            {
                MessageBox.Show("ID produktu musi być liczbą całkowitą.");
                return;
            }
            try
            {
                itemId = int.Parse(TxtUsedProdId.Text);
                if (!_logic.ProductExists(itemId))
                {
                    MessageBox.Show($"Błąd: Produkt o ID {itemId} nie istnieje w magazynie!",
                                    "Nie znaleziono produktu", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd bazy danych: Upewnij się, że produkt o ID {itemId} istnieje w magazynie.\n\nSzczegóły: {ex.Message}",
                                "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            try
            {
                string qtySql = qtyValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                string reason = TxtUsedReason.Text.Trim().Replace("'", "''");
                string insertQuery = $"INSERT INTO UsedProducts (ID_Item, QuantityUsed, Reason) VALUES ({itemId}, {qtySql}, '{reason}')";
                _db.ExecuteNonQuery(insertQuery);
                string updateWarehouse = $"UPDATE Warehouse SET Quantity = Quantity - {qtySql} WHERE ID_Item = {itemId}";
                _db.ExecuteNonQuery(updateWarehouse);
                RefreshAllGrids();
                TxtUsedProdId.Clear();
                TxtUsedQty.Clear();
                TxtUsedReason.Clear();

                MessageBox.Show($"Zarejestrowano zużycie {qtyValue} jednostek produktu ID: {itemId}.", "Sukces");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Błąd bazy danych: Upewnij się, że produkt o ID {itemId} istnieje w magazynie.\n\nSzczegóły: {ex.Message}",
                                "Błąd krytyczny", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void DgUsedProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgUsedProducts.SelectedItem is DataRowView row)
            {
                TxtUsedProdId.Text = row["ID_Item"].ToString();
                TxtUsedQty.Text = row["QuantityUsed"].ToString();
                TxtUsedReason.Text = row["Reason"].ToString();
            }
        }
        private void BtnDeleteUsage_Click(object sender, RoutedEventArgs e)
        {
            if (DgUsedProducts.SelectedItem is DataRowView row)
            {
                if (MessageBox.Show("Czy na pewno chcesz usunąć to zużycie? Stan magazynowy zostanie przywrócony.", "Potwierdzenie", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        int usageId = Convert.ToInt32(row["ID_Usage"]);
                        int itemId = Convert.ToInt32(row["ID_Item"]);
                        decimal qtyUsed = Convert.ToDecimal(row["QuantityUsed"]);
                        string qtySql = qtyUsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        _db.ExecuteNonQuery($"UPDATE Warehouse SET Quantity = Quantity + {qtySql} WHERE ID_Item = {itemId}");
                        _db.ExecuteNonQuery($"DELETE FROM UsedProducts WHERE ID_Usage = {usageId}");
                        RefreshAllGrids();
                        MessageBox.Show("Zużycie usunięte, stan magazynowy przywrócony.");
                    }
                    catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
                }
            }
        }
        private void BtnUpdateUsage_Click(object sender, RoutedEventArgs e)
        {
            if (DgUsedProducts.SelectedItem is DataRowView row)
            {
                try
                {
                    int usageId = Convert.ToInt32(row["ID_Usage"]);
                    int oldItemId = Convert.ToInt32(row["ID_Item"]);
                    decimal oldQty = Convert.ToDecimal(row["QuantityUsed"]);

                    int newItemId = Convert.ToInt32(TxtUsedProdId.Text);
                    decimal newQty = Convert.ToDecimal(TxtUsedQty.Text.Replace(",", "."));
                    string reason = TxtUsedReason.Text.Trim().Replace("'", "''");
                    _db.ExecuteNonQuery($"UPDATE Warehouse SET Quantity = Quantity + {oldQty.ToString(System.Globalization.CultureInfo.InvariantCulture)} WHERE ID_Item = {oldItemId}");
                    _db.ExecuteNonQuery($"UPDATE Warehouse SET Quantity = Quantity - {newQty.ToString(System.Globalization.CultureInfo.InvariantCulture)} WHERE ID_Item = {newItemId}");
                    string updateQuery = $@"UPDATE UsedProducts SET 
                                   ID_Item = {newItemId}, 
                                   QuantityUsed = {newQty.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 
                                   Reason = '{reason}' 
                                   WHERE ID_Usage = {usageId}";
                    _db.ExecuteNonQuery(updateQuery);

                    RefreshAllGrids();
                    MessageBox.Show("Zużycie zaktualizowane pomyślnie.");
                }
                catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
            }
        }
       
        private void BtnCreateOrder_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtOrdSupId.Text))
            {
                MessageBox.Show("Podaj ID Dostawcy!");
                return;
            }

            try
            {
                int supId = Convert.ToInt32(TxtOrdSupId.Text);
                string query = $"INSERT INTO Orders (ID_Supplier, OrderDate, Status) VALUES ({supId}, GETDATE(), 'Nowe')";
                _db.ExecuteNonQuery(query);
                RefreshAllGrids();
                MessageBox.Show("Nagłówek zamówienia utworzony.");
            }
            catch (Exception ex) { MessageBox.Show("Błąd: " + ex.Message); }
        }
        private void BtnUpdateOrder_Click(object sender, RoutedEventArgs e)
        {
            if (DgOrders.SelectedItem is DataRowView row)
            {
                if (string.IsNullOrWhiteSpace(TxtOrdSupId.Text))
                {
                    MessageBox.Show("Podaj ID Dostawcy!");
                    return;
                }

                try
                {
                    int orderId = Convert.ToInt32(row["ID_Order"]);
                    int supId = Convert.ToInt32(TxtOrdSupId.Text);
                    string status = TxtOrdStatus.Text.Trim().Replace("'", "''");
                    string checkSupplierQuery = $"SELECT COUNT(*) FROM Suppliers WHERE ID_Supplier = {supId}";
                    DataTable dt = _db.GetDataTable(checkSupplierQuery);
                    int count = Convert.ToInt32(dt.Rows[0][0]);

                    if (count == 0)
                    {
                        MessageBox.Show($"Błąd: Dostawca o ID {supId} nie istnieje w bazie danych!", "Nie znaleziono dostawcy", MessageBoxButton.OK, MessageBoxImage.Error);
                        return; 
                    }
                    string updateQuery = $"UPDATE Orders SET ID_Supplier = {supId} , Status = '{status}' WHERE ID_Order = {orderId}";
                    _db.ExecuteNonQuery(updateQuery);
                    RefreshAllGrids();
                    MessageBox.Show("Zamówienie zostało zaktualizowane.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Wystąpił nieoczekiwany błąd: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("Wybierz zamówienie z listy do edycji.");
            }
        }
        private void BtnDeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            if (DgOrders.SelectedItem is DataRowView row)
            {
                if (MessageBox.Show("Usunięcie zamówienia spowoduje wycofanie wszystkich jego pozycji z magazynu. Kontynuować?",
                                    "Uwaga!", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        int orderId = Convert.ToInt32(row["ID_Order"]);
                        DataTable details = _db.GetDataTable($"SELECT ID_Item, Quantity FROM OrderDetails WHERE ID_Order = {orderId}");
                        foreach (DataRow detail in details.Rows)
                        {
                            int itemId = Convert.ToInt32(detail["ID_Item"]);
                            string qty = detail["Quantity"].ToString().Replace(",", ".");
                            _db.ExecuteNonQuery($"UPDATE Warehouse SET Quantity = Quantity - {qty} WHERE ID_Item = {itemId}");
                        }
                        _db.ExecuteNonQuery($"DELETE FROM Orders WHERE ID_Order = {orderId}");
                        RefreshAllGrids();
                        MessageBox.Show("Zamówienie usunięte, stany magazynowe zostały skorygowane.");
                    }
                    catch (Exception ex) { MessageBox.Show("Błąd podczas usuwania: " + ex.Message); }
                }
            }
        }
        private void BtnAddDetail_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtDetOrderId.Text))
            {
                MessageBox.Show("Wybierz zamówienie z listy!");
                return;
            }
            if (string.IsNullOrWhiteSpace(TxtDetProdId.Text))
            {
                MessageBox.Show("Podaj ID produktu!");
                return;
            }

            try
            {
                int orderId = int.Parse(TxtDetOrderId.Text);
                int itemId = int.Parse(TxtDetProdId.Text);
                if (!_logic.ProductExists(itemId))
                {
                    MessageBox.Show($"Produkt o ID {itemId} nie istnieje w magazynie!");
                    return;
                }
                string qty = TxtDetQty.Text.Replace(",", ".");
                string price = TxtDetPrice.Text.Replace(",", ".");
                _db.ExecuteNonQuery($"INSERT INTO OrderDetails (ID_Order, ID_Item, Quantity, UnitPrice) VALUES ({orderId}, {itemId}, {qty}, {price})");
                _db.ExecuteNonQuery($"UPDATE Warehouse SET Quantity = Quantity + {qty} WHERE ID_Item = {itemId}");
                UpdateOrderTotalValue(orderId);
                RefreshAllGrids();
                LoadOrderDetails(orderId);
                TxtDetProdId.Clear();
                TxtDetQty.Clear();
                TxtDetPrice.Clear();

                MessageBox.Show("Dodano pozycję do zamówienia.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Wystąpił błąd podczas dodawania: " + ex.Message);
            }
        }
        private void DgOrderDetails_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgOrderDetails.SelectedItem is DataRowView row)
            {
                TxtDetProdId.Text = row["ID_Item"].ToString();
                TxtDetQty.Text = row["Quantity"].ToString();
                TxtDetPrice.Text = row["UnitPrice"].ToString();
            }
        }

        private void UpdateOrderTotalValue(int orderId)
        {
            try
            {
                string sumQuery = $@"
            SELECT SUM(Quantity * UnitPrice) 
            FROM OrderDetails 
            WHERE ID_Order = {orderId}";

                DataTable dt = _db.GetDataTable(sumQuery);
                string totalValue = "0.00";
                if (dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    decimal sum = Convert.ToDecimal(dt.Rows[0][0]);
                    totalValue = sum.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                string updateQuery = $"UPDATE Orders SET TotalValue = {totalValue} WHERE ID_Order = {orderId}";
                _db.ExecuteNonQuery(updateQuery);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Błąd podczas przeliczania sumy zamówienia: " + ex.Message);
            }
        }
        private void BtnDeleteDetail_Click(object sender, RoutedEventArgs e)
        {
            if (DgOrderDetails.SelectedItem is DataRowView row)
            {
                if (MessageBox.Show("Czy na pewno chcesz usunąć tę pozycję?", "Potwierdzenie", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        int detailId = Convert.ToInt32(row["ID_OrderDetail"]);
                        int itemId = Convert.ToInt32(row["ID_Item"]);
                        decimal qty = Convert.ToDecimal(row["Quantity"]);
                        if (string.IsNullOrEmpty(TxtDetOrderId.Text))
                        {
                            MessageBox.Show("Błąd: Nie wybrano zamówienia!");
                            return;
                        }
                        int orderId = int.Parse(TxtDetOrderId.Text);
                        string qtySql = qty.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        _db.ExecuteNonQuery($"UPDATE Warehouse SET Quantity = Quantity - {qtySql} WHERE ID_Item = {itemId}");
                        _db.ExecuteNonQuery($"DELETE FROM OrderDetails WHERE ID_OrderDetail = {detailId}");
                        UpdateOrderTotalValue(orderId); 
                        RefreshAllGrids();
                        LoadOrderDetails(orderId);

                        MessageBox.Show("Pozycja usunięta, stan magazynowy zaktualizowany.");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Błąd podczas usuwania: " + ex.Message);
                    }
                }
            }
            else
            {
                MessageBox.Show("Zaznacz pozycję w dolnej tabeli, którą chcesz usunąć!");
            }
        }
        private void BtnUpdateDetail_Click(object sender, RoutedEventArgs e)
        {
            if (DgOrderDetails.SelectedItem is DataRowView row)
            {
                try
                {
                    int detailId = Convert.ToInt32(row["ID_OrderDetail"]);
                    int orderId = int.Parse(TxtDetOrderId.Text);
                    int oldItemId = Convert.ToInt32(row["ID_Item"]);
                    decimal oldQty = Convert.ToDecimal(row["Quantity"]); 
                    if (string.IsNullOrWhiteSpace(TxtDetProdId.Text)) { MessageBox.Show("Podaj ID towaru!"); return; }
                    int newItemId = int.Parse(TxtDetProdId.Text);
                    if (!_logic.ProductExists(newItemId))
                    {
                        MessageBox.Show("Produkt o podanym ID nie istnieje w magazynie!");
                        return;
                    }

                    decimal newQty = decimal.Parse(TxtDetQty.Text.Replace(",", "."));
                    decimal newPrice = decimal.Parse(TxtDetPrice.Text.Replace(",", "."));
                    orderId = int.Parse(TxtDetOrderId.Text);
                    string oldQtySql = oldQty.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _db.ExecuteNonQuery($"UPDATE Warehouse SET Quantity = Quantity - {oldQtySql} WHERE ID_Item = {oldItemId}");
                    string newQtySql = newQty.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    _db.ExecuteNonQuery($"UPDATE Warehouse SET Quantity = Quantity + {newQtySql} WHERE ID_Item = {newItemId}");
                    string updateQuery = $@"UPDATE OrderDetails SET 
                                   ID_Item = {newItemId}, 
                                   Quantity = {newQtySql}, 
                                   UnitPrice = {newPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)} 
                                   WHERE ID_OrderDetail = {detailId}";

                    _db.ExecuteNonQuery(updateQuery);
                    UpdateOrderTotalValue(orderId);
                    RefreshAllGrids();
                    LoadOrderDetails(orderId);

                    MessageBox.Show("Pozycja została zaktualizowana, a stany magazynowe przeliczone.");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Błąd podczas aktualizacji: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("Zaznacz pozycję w dolnej tabeli, którą chcesz edytować!");
            }
        }

    }
}