using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Sync;

namespace TreeViewWithCheckBoxes
{
    public class FooViewModel : INotifyPropertyChanged
    {
        private const string ICON_VIRTUALROOT = "Images/virtualroot.png";
        private const string ICON_PLACEHOLDER = "Images/placeholder.png";
        private const string ICON_FOLDER_CLOSE = "Images/folder_close.png";
        private const string ICON_FOLDER_OPEN = "Images/folder_open.png";
        private const string ICON_FILE = "Images/file.png";

        private enum ItemType
        {
            ITEM_TYPE_VIRTUALROOT,
            ITEM_TYPE_PLACEHOLDER,
            ITEM_TYPE_FOLDER,
            ITEM_TYPE_FILE,
        }
        #region Data

        ItemType _itemType;
        string _fullpath;           // 此变量若有值，则表示其指向的目录内容尚未加载进来
        bool? _isChecked = false;
        bool _isExpanded = false;
        public FooViewModel _parent;

        #endregion // Data

        ISimpleFS _rootFS;

        // 创建根项
        public static FooViewModel CreateRootItem( string name, ISimpleFS rootFS )
        {
            FooViewModel item = new FooViewModel( name, rootFS ) {
                _itemType = ItemType.ITEM_TYPE_VIRTUALROOT,
                Icon = ICON_VIRTUALROOT,
            };
            return item;
        }

        // 创建一个目录子项（用懒加载）
        public FooViewModel CreateLazyFolderItem( string name, string fullpath )
        {
            FooViewModel subItem = new FooViewModel( name, this._rootFS ) {
                _parent = this,
                _itemType = ItemType.ITEM_TYPE_FOLDER,
                Icon = ICON_FOLDER_CLOSE,
            };
            if ( _isChecked.HasValue && (bool)_isChecked ) {
                subItem._isChecked = true;
            }

            subItem._fullpath = fullpath;
            subItem.Children.Add( new FooViewModel( "", this._rootFS ) {
                _itemType = ItemType.ITEM_TYPE_PLACEHOLDER,
                Icon = ICON_PLACEHOLDER,
                CbVisibility = "Collapsed",
            } );
            return subItem;
        }

        // 创建一个目录子项（不用懒加载）
        public FooViewModel CreateFolderItem( string name )
        {
            FooViewModel subItem = new FooViewModel( name, this._rootFS ) {
                _parent = this,
                _itemType = ItemType.ITEM_TYPE_FOLDER,
                Icon = ICON_FOLDER_CLOSE,
            };
            if ( _isChecked.HasValue && (bool)_isChecked ) {
                subItem._isChecked = true;
            }
            return subItem;
        }

        // 创建一个文件子项
        public FooViewModel CreateFileItem( string name )
        {
            FooViewModel subItem = new FooViewModel( name, this._rootFS ) {
                _parent = this,
                _itemType = ItemType.ITEM_TYPE_FILE,
                Icon = ICON_FILE,
            };
            if ( _isChecked.HasValue && (bool)_isChecked ) {
                subItem._isChecked = true;
            }
            return subItem;
        }

        public FooViewModel( string name, ISimpleFS rootFS )
        {
            this._fullpath = "";
            this.Name = name;
            this.Children = new List<FooViewModel>();
            this.CbVisibility = "Visible";
            this._rootFS = rootFS;
        }

        #region Properties

        public List<FooViewModel> Children { get; private set; }

        public bool IsInitiallySelected { get; set; }

        public string Name { get; private set; }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if ( value != _isExpanded ) {
                    _isExpanded = value;
                    this.OnPropertyChanged( "IsExpanded" );
                }

                // Expand all the way up to the root.
                if ( _isExpanded && _parent != null )
                    _parent.IsExpanded = true;

                if ( _isExpanded ) {
                    if ( _fullpath.Length > 0 ) {
                        SortedList<string, SimpleInfoBase> children = this._rootFS.getChildren( _fullpath );
                        Children.Clear();

                        foreach ( KeyValuePair<string, SimpleInfoBase> item in children ) {
                            if ( item.Value.GetType() == typeof( SimpleDirInfo ) ) {
                                Children.Add( CreateLazyFolderItem( item.Value.Name, item.Value.FullName ) );
                            }
                        }

                        foreach ( KeyValuePair<string, SimpleInfoBase> item in children ) {
                            if ( item.Value.GetType() == typeof( SimpleFileInfo ) ) {
                                Children.Add( CreateFileItem( item.Value.Name ) );
                            }
                        }

                        _fullpath = "";
                    }

                    if ( _itemType == ItemType.ITEM_TYPE_FOLDER ) {
                        Icon = ICON_FOLDER_OPEN;
                        this.OnPropertyChanged( "Icon" );
                    }
                } else {
                    if ( _itemType == ItemType.ITEM_TYPE_FOLDER ) {
                        Icon = ICON_FOLDER_CLOSE;
                        this.OnPropertyChanged( "Icon" );
                    }
                }
            }
        }

        public string Icon { get; set; }

        public string CbVisibility { get; set; }

        #region IsChecked

        /// <summary>
        /// Gets/sets the state of the associated UI toggle (ex. CheckBox).
        /// The return value is calculated based on the check state of all
        /// child FooViewModels.  Setting this property to true or false
        /// will set all children to the same check state, and setting it 
        /// to any value will cause the parent to verify its check state.
        /// </summary>
        public bool? IsChecked
        {
            get { return _isChecked; }
            set { this.SetIsChecked( value, true, true ); }
        }

        void SetIsChecked( bool? value, bool updateChildren, bool updateParent )
        {
            if ( value == _isChecked )
                return;

            _isChecked = value;

            if ( updateChildren && _isChecked.HasValue )
                this.Children.ForEach( c => c.SetIsChecked( _isChecked, true, false ) );

            if ( updateParent && _parent != null )
                _parent.VerifyCheckState();

            this.OnPropertyChanged( "IsChecked" );
        }

        void VerifyCheckState()
        {
            bool? state = null;
            for ( int i = 0; i < this.Children.Count; ++i ) {
                bool? current = this.Children[i].IsChecked;
                if ( i == 0 ) {
                    state = current;
                } else if ( state != current ) {
                    state = null;
                    break;
                }
            }
            this.SetIsChecked( state, false, true );
        }

        #endregion // IsChecked

        #endregion // Properties

        #region INotifyPropertyChanged Members

        void OnPropertyChanged( string prop )
        {
            if ( this.PropertyChanged != null )
                this.PropertyChanged( this, new PropertyChangedEventArgs( prop ) );
        }

        public event PropertyChangedEventHandler PropertyChanged;

        #endregion
    }
}
