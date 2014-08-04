using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PshSize = System.Management.Automation.Host.Size;
using PshRectangle = System.Management.Automation.Host.Rectangle;

/*
 * 
   ConsoleColor     BackgroundColor { get; set; }
   Size             BufferSize { get; set; }
   Coordinates      CursorPosition { get; set; }
   int              CursorSize { get; set; }
   ConsoleColor     ForegroundColor { get; set; }
   bool             KeyAvailable { get; }
   Size             MaxPhysicalWindowSize { get; }
   Size             MaxWindowSize { get; }
   Coordinates      WindowPosition { get; set; }
   Size             WindowSize { get; set; }
   string           WindowTitle { get; set; }

   void                 FlushInputBuffer();
   BufferCell[,]        GetBufferContents(Rectangle rectangle);
   int                  LengthInBufferCells(char source);
   int                  LengthInBufferCells(string source);
   int                  LengthInBufferCells(string source, int offset);
   BufferCell[,]        NewBufferCellArray(Size size, BufferCell contents);
   BufferCell[,]        NewBufferCellArray(int width, int height, BufferCell contents);
   BufferCell[,]        NewBufferCellArray(string[] contents, ConsoleColor foregroundColor, ConsoleColor backgroundColor);
   KeyInfo              ReadKey();
   KeyInfo              ReadKey(ReadKeyOptions options);
   void                 ScrollBufferContents(Rectangle source, Coordinates destination, Rectangle clip, BufferCell fill);
   void                 SetBufferContents(Coordinates origin, BufferCell[,] contents);
   void                 SetBufferContents(Rectangle rectangle, BufferCell fill);
*/

namespace Sean
{
    /// <summary>
    /// A sample implementation of the PSHostRawUserInterface for console
    /// applications. Members of this class that easily map to the .NET 
    /// console class are implemented. More complex methods are not 
    /// implemented and throw a NotImplementedException exception.
    /// </summary>
    public class MyPSHostRawUserInterface : PSHostRawUserInterface
    {
        MainWindow mainwin;

        public MyPSHostRawUserInterface(MainWindow mainwin)
        {
            this.mainwin = mainwin;
            ForegroundColor = ConsoleColor.Green;
            BackgroundColor = ConsoleColor.Black;
            WindowTitle = "Sean the Shell";
        }

        #region Colours

        /// <summary>
        /// Gets or sets the foreground color of the text to be written.
        /// </summary>
        public override ConsoleColor ForegroundColor
        {
            get;
            set;
        }

        /// <summary>
        /// Gets or sets the background color of text to be written.
        /// </summary>
        public override ConsoleColor BackgroundColor
        {
            get;
            set;
        }

        #endregion

        /// <summary>
        /// Gets or sets the host buffer size adapted from the Console buffer 
        /// size members.
        /// </summary>
        public override PshSize BufferSize
        {
            get { return new PshSize(130,mainwin.maxlines); }
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Gets or sets the cursor position. In this example this 
        /// functionality is not needed so the property throws a 
        /// NotImplementException exception.
        /// </summary>
        public override Coordinates CursorPosition
        {
            get 
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
            set
            {
                throw new NotImplementedException("The method or operation is not implemented.");
            }
        }

        /// <summary>
        /// Gets or sets the cursor size 
        /// </summary>
        public override int CursorSize
        {
            get;
            set;
        }


        /// <summary>
        /// Gets a value indicating whether a key is available. This maps to  
        /// </summary>
        public override bool KeyAvailable
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Gets the maximum physical size of the window adapted from the  
        /// properties.
        /// </summary>
        public override PshSize MaxPhysicalWindowSize
        {
            get { return new PshSize(130,mainwin.maxlines); }
        }

        /// <summary>
        /// Gets the maximum window size adapted from the 
        /// </summary>
        public override PshSize MaxWindowSize
        {
            get { return MaxPhysicalWindowSize; }
        }

        /// <summary>
        /// Gets or sets the window position adapted from the Console window position 
        /// members.
        /// </summary>
        public override Coordinates WindowPosition
        {
            get { throw new NotImplementedException(); }
            set { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Gets or sets the window size adapted from the corresponding Console 
        /// calls.
        /// </summary>
        public override PshSize WindowSize
        {
            get { return BufferSize; }
            set { BufferSize = value; }
        }

        /// <summary>
        /// Gets or sets the title of the window 
        /// </summary>
        public override string WindowTitle
        {
            get { return titleCache; }
            set
            {
                titleCache = value;
                mainwin.Dispatcher.BeginInvoke(new Action<string>((v) => mainwin.SetTitle(v)), 
                                                DispatcherPriority.Input, (string)value.Clone());
            }
        }

        String titleCache;

        /// <summary>
        /// This API resets the input buffer. In this example this 
        /// functionality is not needed so the method returns nothing.
        /// </summary>
        public override void FlushInputBuffer()
        {
        }

        /// <summary>
        /// This API returns a rectangular region of the screen buffer. In 
        /// this example this functionality is not needed so the method throws 
        /// a NotImplementException exception.
        /// </summary>
        /// <param name="rectangle">Defines the size of the rectangle.</param>
        /// <returns>Throws a NotImplementedException exception.</returns>
        public override BufferCell[,] GetBufferContents(PshRectangle rectangle)
        {
            throw new NotImplementedException(
                     "The method or operation is not implemented.");
        }

        /// <summary>
        /// This API Reads a pressed, released, or pressed and released keystroke 
        /// from the keyboard device, blocking processing until a keystroke is 
        /// typed that matches the specified keystroke options. In this example 
        /// this functionality is not needed so the method throws a
        /// NotImplementException exception.
        /// </summary>
        /// <param name="options">Options, such as IncludeKeyDown,  used when 
        /// reading the keyboard.</param>
        /// <returns>Throws a NotImplementedException exception.</returns>
        public override KeyInfo ReadKey(ReadKeyOptions options)
        {
            throw new NotImplementedException(
                      "The method or operation is not implemented.");
        }

        /// <summary>
        /// This API crops a region of the screen buffer. In this example 
        /// this functionality is not needed so the method throws a
        /// NotImplementException exception.
        /// </summary>
        /// <param name="source">The region of the screen to be scrolled.</param>
        /// <param name="destination">The region of the screen to receive the 
        /// source region contents.</param>
        /// <param name="clip">The region of the screen to include in the operation.</param>
        /// <param name="fill">The character and attributes to be used to fill all cell.</param>
        public override void ScrollBufferContents(PshRectangle source, Coordinates destination, PshRectangle clip, BufferCell fill)
        {
            throw new NotImplementedException(
                      "The method or operation is not implemented.");
        }

        /// <summary>
        /// This API copies an array of buffer cells into the screen buffer 
        /// at a specified location. In this example this  functionality is 
        /// not needed si the method  throws a NotImplementedException exception.
        /// </summary>
        /// <param name="origin">The parameter is not used.</param>
        /// <param name="contents">The parameter is not used.</param>
        public override void SetBufferContents(Coordinates origin, BufferCell[,] contents)
        {
            throw new NotImplementedException(
                      "The method or operation is not implemented.");
        }

        /// <summary>
        /// This API Copies a given character, foreground color, and background 
        /// color to a region of the screen buffer. In this example this 
        /// functionality is not needed so the method throws a
        /// NotImplementException exception./// </summary>
        /// <param name="rectangle">Defines the area to be filled. </param>
        /// <param name="fill">Defines the fill character.</param>
        public override void SetBufferContents(PshRectangle rectangle, BufferCell fill)
        {
            throw new NotImplementedException(
                      "The method or operation is not implemented.");
        }
    }
}