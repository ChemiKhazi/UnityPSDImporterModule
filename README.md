Unity Psd Importer Submodule
==================

## ⛔⚠️ Deprecation Alert ⚠️⛔

This project is considered deprecated and unsupported. Work is moving to the [Psd2Unity importer](https://github.com/ChemiKhazi/Psd2UnityImporter). This repo is being kept for documentation purposes.

⛔⚠️ **End warning** ⛔⚠️

This is the submodule for the [Unity Psd Importer](https://github.com/ChemiKhazi/UnityPsdImporter). If you're looking for an easy to install version of the importer, please visit the main project page.

Contributing
------------

It is recommended to use this repository as a submodule inside a Unity project when developing on this project.

To allow Unity to compile the project inside the editor, place the following two files in the root `Assets` directory of your Unity project.

- [gmcs.rsp](https://raw.githubusercontent.com/ChemiKhazi/UnityPsdImporter/master/PhotoShopFileType/gmcs.rsp)
- [smcs.rsp](https://raw.githubusercontent.com/ChemiKhazi/UnityPsdImporter/master/PhotoShopFileType/smcs.rsp)

Extensions
----------

The PSD Importer can be [extended to reconstruct](Editor/Reconstructor) PSDs for other UI systems. When developing these extensions, it is recommended to download the DLL for development and to keep the extension script outside of this git repository.
