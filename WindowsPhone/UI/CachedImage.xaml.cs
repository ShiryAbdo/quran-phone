﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Shell;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows.Media.Imaging;
using QuranPhone.Utils;
using ImageTools;
using System.IO.IsolatedStorage;
using System.IO;
using System.Diagnostics;

namespace QuranPhone.UI
{
    public partial class CachedImage : UserControl, INotifyPropertyChanged
    {
        private BitmapImage imageSource;

        public CachedImage()
        {
            imageSource = new BitmapImage();
            InitializeComponent();
        }

        public Uri ImageSource {
            get { return (Uri)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); } 
        }

        public static readonly DependencyProperty ImageSourceProperty = DependencyProperty.Register("ImageSource",
            typeof(Uri), typeof(CachedImage), new PropertyMetadata(
            new PropertyChangedCallback(changeSource))); 
        
        private static void changeSource(DependencyObject source, DependencyPropertyChangedEventArgs e) 
        { 
            (source as CachedImage).UpdateSource(e.NewValue as Uri); 
        }

        private void UpdateSource(Uri source)
        {
            imageSource.ImageOpened += imageSource_ImageOpened;
            if (source.Scheme == "isostore")
            {
                var isf = IsolatedStorageFile.GetUserStoreForApplication();
                using (var stream = isf.OpenFile(source.LocalPath, FileMode.Open))
                {
                    imageSource.SetSource(stream);
                }
            }
            else
            {
                imageSource.UriSource = source;
            }
            image.Source = imageSource;
        }

        void imageSource_ImageOpened(object sender, RoutedEventArgs e)
        {
            UriBuilder uriBuilder = new UriBuilder(ImageSource);
            var path = Path.Combine(QuranFileUtils.GetQuranDirectory(false), Path.GetFileName(uriBuilder.Path));
                        
            try
            {
                var isf = IsolatedStorageFile.GetUserStoreForApplication();
                if (!isf.FileExists(path))
                {
                    WriteableBitmap writableBitmap = new WriteableBitmap(imageSource);
                    var encoder = new ImageTools.IO.Png.PngEncoder();
                    using (var isfStream = new IsolatedStorageFileStream(path, FileMode.Create, isf))
                    {
                        encoder.Encode(writableBitmap.ToImage(), isfStream);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("failed to store file {0}: {1}", path, ex.Message);
            }
        }

        #region INotifyPropertyChanged Members

        /// <summary>
        /// Raised when a property on this object has a new value.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        protected virtual void OnPropertyChanged<T>(Expression<Func<T>> expression)
        {
            string propertyName = GetPropertyName(expression);
            OnPropertyChanged(propertyName);
        }

        /// <summary>
        /// Raises this object's PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property that has a new value.</param>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        protected virtual bool IsProperty<T>(Expression<Func<T>> expression, string name)
        {
            string propertyName = GetPropertyName(expression);
            return propertyName == name;
        }

        /// <summary>
        /// Get the string name for the property
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expression"></param>
        /// <returns></returns>
        protected string GetPropertyName<T>(Expression<Func<T>> expression)
        {
            MemberExpression memberExpression = (MemberExpression)expression.Body;
            return memberExpression.Member.Name;
        }

        #endregion // INotifyPropertyChanged Members
    }
}