using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using System.Windows.Forms;
using System.Xml.Schema;
using System.Xml;

namespace CarSalesApp
{
    // Class for car sales
    public class CarSale
    {
        public string Model { get; set; }
        public DateTime SaleDate { get; set; }
        public double Price { get; set; }
        public double VAT { get; set; }
    }

    // Class for summarized sales
    public class SummarizedSale
    {
        public string Model { get; set; }
        public double TotalPriceWithoutVAT { get; set; }
        public double TotalPriceWithVAT { get; set; }
    }

    public partial class MainWindow : Window
    {
        private List<CarSale> allSales;
        private List<SummarizedSale> summarizedSales;
        private Dictionary<string, DataGridColumn> columnsDictionary;
        public const string XSD_PATH = "../../validate.xsd"; // Path to XSD file

        public MainWindow()
        {
            InitializeComponent();
            ToggleViewButton.Visibility = Visibility.Collapsed;
            SaveXmlButton.Visibility = Visibility.Collapsed;
            // Columns dictionary for easier access by name
            columnsDictionary = new Dictionary<string, DataGridColumn>();
            foreach (var column in SalesDataGrid.Columns)
                columnsDictionary[column.Header.ToString()] = column;
        }

        private void ProcessXmlFile(string filePath)
        {
            if (!IsXmlValid(filePath))
                return;

            XDocument doc = XDocument.Load(filePath);
            // Parse all car sales
            allSales = doc.Descendants("Auto")
                .Select(car => new CarSale
                {
                    Model = car.Element("Model")?.Value,
                    SaleDate = DateTime.ParseExact(car.Element("DatumProdeje")?.Value ?? string.Empty, "dd.MM.yyyy", CultureInfo.InvariantCulture),
                    Price = double.Parse(car.Element("Cena")?.Value ?? "0"),
                    VAT = double.Parse(car.Element("DPH")?.Value ?? "0")
                })
                .ToList();

            // Filter and group weekend sales
            summarizedSales = allSales
                .Where(car => car.SaleDate.DayOfWeek == DayOfWeek.Saturday || car.SaleDate.DayOfWeek == DayOfWeek.Sunday)
                .GroupBy(car => car.Model)
                .Select(group => new SummarizedSale
                {
                    Model = group.Key,
                    TotalPriceWithoutVAT = group.Sum(car => car.Price),
                    TotalPriceWithVAT = group.Sum(car => car.Price * (1 + car.VAT / 100))
                })
                .ToList();

            // Display summarized data by default
            DisplaySummarizedData();
            ToggleViewButton.Visibility = Visibility.Visible;
            SaveXmlButton.Visibility = Visibility.Visible;
        }

        private void DisplayFullData()
        {
            SalesDataGrid.ItemsSource = allSales;
            ToggleViewButton.Content = "Zobrazit výslednou tabulku"; // Update button content to match view
            // Show full data columns
            columnsDictionary["Datum prodeje"].Visibility = Visibility.Visible;
            columnsDictionary["Cena"].Visibility = Visibility.Visible;
            columnsDictionary["DPH"].Visibility = Visibility.Visible;
            // Hide summarized data columns
            columnsDictionary["Cena bez DPH"].Visibility = Visibility.Collapsed;
            columnsDictionary["Cena s DPH"].Visibility = Visibility.Collapsed;
        }

        private void DisplaySummarizedData()
        {
            SalesDataGrid.ItemsSource = summarizedSales;
            ToggleViewButton.Content = "Zobrazit celou tabulku"; // Update button content to match view
            // Hide full data columns
            columnsDictionary["Datum prodeje"].Visibility = Visibility.Collapsed;
            columnsDictionary["Cena"].Visibility = Visibility.Collapsed;
            columnsDictionary["DPH"].Visibility = Visibility.Collapsed;
            // Show summarized data columns
            columnsDictionary["Cena bez DPH"].Visibility = Visibility.Visible;
            columnsDictionary["Cena s DPH"].Visibility = Visibility.Visible;
        }

        private void LoadXmlButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "XML Files (*.xml)|*.xml";

            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                ProcessXmlFile(openFileDialog.FileName);
        }
        private void ToggleViewButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle between full data and summarized data
            if (SalesDataGrid.ItemsSource == summarizedSales)
                DisplayFullData();
            else
                DisplaySummarizedData();
        }

        private void SaveXmlButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XML Files (*.xml)|*.xml";

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                SaveDataToXml(saveFileDialog.FileName);
        }

        private void SaveDataToXml(string filePath)
        {
            XDocument doc = new XDocument();
            if (SalesDataGrid.ItemsSource.GetType() == typeof(List<CarSale>))
            {
                var sales = (List<CarSale>)SalesDataGrid.ItemsSource;
                doc.Add(new XElement("ProdejeAut",
                    sales.Select(sale =>
                        new XElement("Auto",
                            new XElement("Model", sale.Model),
                            new XElement("DatumProdeje", sale.SaleDate.ToString("dd.MM.yyyy")),
                            new XElement("Cena", sale.Price.ToString(CultureInfo.InvariantCulture)),
                            new XElement("DPH", sale.VAT.ToString(CultureInfo.InvariantCulture))
                        )
                    )
                ));
            }
            else
            {
                var sales = (List<SummarizedSale>)SalesDataGrid.ItemsSource;
                doc.Add(new XElement("ProdejeAutVysledne",
                    sales.Select(sale =>
                        new XElement("Auto",
                            new XElement("Model", sale.Model),
                            new XElement("CelkováCenaBezDPH", sale.TotalPriceWithoutVAT.ToString(CultureInfo.InvariantCulture)),
                            new XElement("CelkováCenaSDPH", sale.TotalPriceWithVAT.ToString(CultureInfo.InvariantCulture))
                        )
                    )
                ));
            }
            doc.Save(filePath);
            IsXmlValid(filePath); // inform if stored XML is invalid
        }

        static bool IsXmlValid(string xmlPath)
        {
            XmlDocument xml = new XmlDocument();
            try
            {
                xml.Load(xmlPath);
                xml.Schemas.Add(null, XSD_PATH);
                xml.Validate(null);
            }
            catch (XmlSchemaValidationException)
            {
                System.Windows.MessageBox.Show("XML is not valid", "XML Validation Result", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;
            }
            return true;
        }
    }
}
