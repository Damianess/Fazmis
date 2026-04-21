using System.Windows;
using System.IO;
using Microsoft.Extensions.Configuration;
using Fazmis;
using System.Data;

namespace FazmisUi
{
    public partial class MainWindow : Window
    {
        private readonly FazmisLogic _logic;

        public MainWindow()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(System.AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();
            _logic = new FazmisLogic(config.GetConnectionString("FazmisDb"));
            InitializeComponent();
            _logic.SetupApp();
            RefreshData();
        }

        private void RefreshData()
        {
            dgWarehouse.ItemsSource = _logic.GetWarehouseData().DefaultView;
            dgSuppliers.ItemsSource = _logic.GetSuppliersData().DefaultView;
        }
        private void BtnLoadWarehouse_Click(object sender, RoutedEventArgs e) => RefreshData();
        private void BtnLoadSuppliers_Click(object sender, RoutedEventArgs e) => RefreshData();
        private void BtnDeleteSupplier_Click(object sender, RoutedEventArgs e)
        {
            if (dgSuppliers.SelectedItem is DataRowView row)
            {
                _logic.DeleteSupplier(row["Name"].ToString());
                RefreshData();
            }
            else
            {
                MessageBox.Show("Proszę wybrać dostawcę z listy do usunięcia.");
            }
        }
        private void BtnAddProduct_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Funkcja dodawania produktu zostanie zaimplementowana wkrótce.");
        private void BtnDeleteProduct_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Funkcja usuwania produktu zostanie zaimplementowana wkrótce.");
        private void BtnAddSupplier_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show("Funkcja dodawania dostawcy zostanie zaimplementowana wkrótce.");
    }
}