﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace RevitClasher
{
    /// <summary>
    /// Interaction logic for MainUserControl.xaml
    /// </summary>
    public partial class MainUserControl : Window
    {

        private ExternalEvent m_ExEvent;
        private ExternalEventClashDetection m_Handler;
        public static bool _wasExecuted;
        public static MainUserControl thisForm;
        public static ObservableCollection<RevitElement> elementsClashingB { get; set; }
        public static ObservableCollection<RevitElement> elementsClashingA { get; set; }
        public static bool _Reset = false;


        public MainUserControl(ExternalEvent exEvent, ExternalEventClashDetection handler)
        {
            InitializeComponent();
            m_ExEvent = exEvent;
            m_Handler = handler;
            FillForm();

            this.DataContext = this;

            elementsClashingA = new ObservableCollection<RevitElement>();

            elementsClashingA.CollectionChanged += updateA;

            elementsClashingB = new ObservableCollection<RevitElement>();

            elementsClashingB.CollectionChanged += updateB;
            this.Topmost = true;
        }

        private void updateA(object sender, NotifyCollectionChangedEventArgs e)
        {
            ClashesA.Items.Add(MainUserControl.elementsClashingA.Last());

        }
        private void updateB(object sender, NotifyCollectionChangedEventArgs e)
        {
            ClashesB.Items.Add(MainUserControl.elementsClashingB.Last());

        }

        public void FillForm()
        {

            //Fill combo with linked documents
            foreach (var item in Clash.Documents(RevitTools.Doc, RevitTools.App))
            {
                ListOfLinks.Items.Add(item.Key);
            }
            var SelectionA = new List<CheckBox>();
            var SelectionB = new List<CheckBox>();
            var categories = FormTools.ListOfCategories(RevitTools.Doc);
            foreach (var c in categories)
            {
                //List of 
                CheckBox chbox = new CheckBox();
                chbox.Content = c.Key;
                SelectionB.Add(chbox);
                //   SelectionAList.Items.Add(chbox);

            }

            foreach (var c in categories)
            {
                //List of 
                CheckBox chbox = new CheckBox();
                chbox.Content = c.Key;
                SelectionA.Add(chbox);
                //   SelectionAList.Items.Add(chbox);

            }
            SelectionAList.ItemsSource = SelectionA;
            SelectionBList.ItemsSource = SelectionB;

            //Presetting everything from a previous run
            foreach (var item in RevitClasher.Properties.Settings.Default.SelectionB)
            {
                int i = int.Parse(item);
                CheckBox chbox = SelectionBList.Items[i] as CheckBox;
                chbox.IsChecked = true;
            }
            foreach (var item in RevitClasher.Properties.Settings.Default.SelectionA)
            {
                int i = int.Parse(item);
                CheckBox chbox = SelectionAList.Items[i] as CheckBox;
                chbox.IsChecked = true;
            }
            ListOfLinks.SelectedIndex = Properties.Settings.Default.ListOfLinks;

        }



        private void SaveConfiguration()
        {
            Properties.Settings.Default.ListOfLinks = ListOfLinks.SelectedIndex;
            StringCollection selectionA = new StringCollection();
            var listA = ((IEnumerable<CheckBox>)SelectionAList.ItemsSource);
            var listB = ((IEnumerable<CheckBox>)SelectionBList.ItemsSource);
            for (int i = 0; i < listA.Count(); i++)
            {
                CheckBox chbox = listA.ElementAt(i) as CheckBox;
                if (chbox.IsChecked.Value)
                {
                    selectionA.Add(i.ToString());
                }
            }
            Properties.Settings.Default.SelectionA = selectionA;


            StringCollection selectionB = new StringCollection();
            for (int i = 0; i < listB.Count(); i++)
            {
                CheckBox chbox = listB.ElementAt(i) as CheckBox;
                if (chbox.IsChecked.Value)
                {
                    selectionB.Add(i.ToString());
                }
            }
            Properties.Settings.Default.SelectionB = selectionB;
            Properties.Settings.Default.Save();
        }

        private void Run_Click(object sender, RoutedEventArgs e)
        {
            this.ClashesA.Items.Clear();
            this.ClashesB.Items.Clear();
            _Reset = false;
            //Save Selection
            SaveConfiguration();
            _wasExecuted = false;
            var index = Properties.Settings.Default.ListOfLinks;
            var l = Clash.Documents(RevitTools.Doc, RevitTools.App);
            // Get document from select index
            FormTools.linkedDocument = l.Values[index];
            FormTools.SelectedCategories = new List<ElementFilter>();
            FormTools.SelectedHostCategories = new List<ElementFilter>();

            //Categories
            var cat = FormTools.ListOfCategories(RevitTools.Doc);

            //Get categories from select index
            foreach (var i in Properties.Settings.Default.SelectionB)
            {
                int item = int.Parse(i);
                BuiltInCategory myCatEnum = (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), cat.Values[item].Id.ToString());
                FormTools.SelectedCategories.Add(new ElementCategoryFilter(myCatEnum));


            }

            //Get categories from select index
            foreach (var i in Properties.Settings.Default.SelectionA)
            {
                int item = int.Parse(i);
                BuiltInCategory myCatEnum = (BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), cat.Values[item].Id.ToString());
                FormTools.SelectedHostCategories.Add(new ElementCategoryFilter(myCatEnum));
                //Saving the local categories for a new run


            }
            m_ExEvent.Raise();



        }



        private void OnSelectedA(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ClashesA.SelectedItem != null)
                {
                    var vRVTElement = (RevitElement)ClashesA.SelectedItem;

                    RevitTools.Focus(vRVTElement.element.Id.IntegerValue);
                }
            }
            catch
            {

            }
        }


        private void OnSelectedB(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ClashesB.SelectedItem != null)
                {
                    var vRVTElement = (RevitElement)ClashesB.SelectedItem;

                    RevitTools.Focus(vRVTElement.element.Id.IntegerValue);
                }
            }
            catch
            {

            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _Reset = true;
                this.ClashesA.Items.Clear();
                this.ClashesB.Items.Clear();
                m_ExEvent.Raise();

            }
            catch( Exception vEx)
            {
                MessageBox.Show(vEx.Message);
            }
        }

        private void SearchA_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SelectionAList.ItemsSource != null)
            {
                CollectionView view = CollectionViewSource.GetDefaultView(SelectionAList.ItemsSource) as CollectionView;
                view.Filter = CategoryFilterA;

            }

        }
        private bool CategoryFilterA(object item)
        {


            CheckBox Items = (CheckBox)item;
            return Items.Content.ToString().ToUpper().Contains(SearchA.Text.ToUpper());
        }
        private void SearchB_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SelectionBList.ItemsSource != null)
            {
                CollectionView view = CollectionViewSource.GetDefaultView(SelectionBList.ItemsSource) as CollectionView;
                view.Filter = CategoryFilterB;

            }
        }

        private bool CategoryFilterB(object item)
        {
            CheckBox Items = (CheckBox)item;
            return Items.Content.ToString().ToUpper().Contains(SearchB.Text.ToUpper());
        }

    }

}
