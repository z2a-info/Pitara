using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace PitaraApp.Controls
{
    public class CustomUniformGrid:UniformGrid
    {


        public double  ItemWidth
        {
            get { return (double )GetValue(ItemHeightProperty); }
            set { SetValue(ItemHeightProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ItemWidth.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ItemHeightProperty =
            DependencyProperty.Register("ItemWidth", typeof(double ), typeof(CustomUniformGrid));


        public CustomUniformGrid()
        {
            
            this.SizeChanged -= CustomUniformGrid_SizeChanged;
            this.SizeChanged += CustomUniformGrid_SizeChanged;
        }
        
        public override void OnApplyTemplate()
        {
            this.SizeChanged -= CustomUniformGrid_SizeChanged;
            this.SizeChanged += CustomUniformGrid_SizeChanged;

            base.OnApplyTemplate();
            CustomUniformGrid_SizeChanged(null,null);
        }

        private void CustomUniformGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            int col = (int)(this.ActualWidth / ItemWidth);
            if (col!=Columns)
            {
                Columns = col;
            }
        }
    }
}
