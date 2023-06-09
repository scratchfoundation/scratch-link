# Changelog

All notable changes to this project will be documented in this file. See
[Conventional Commits](https://conventionalcommits.org) for commit guidelines.

## [2.0.2-develop.2](https:/home/circleci/project/semantic-release-remote/compare/v2.0.2-develop.1...v2.0.2-develop.2) (2023-06-09)


### Bug Fixes

* **Win:** fix sizing problem in context menu ([c35c934](https:/home/circleci/project/semantic-release-remote/commit/c35c93496a58349c0fd9b0341a23a94cb4107e36))

## [2.0.2-develop.1](https:/home/circleci/project/semantic-release-remote/compare/v2.0.1...v2.0.2-develop.1) (2023-06-08)


### Bug Fixes

* **WinBLE:** disconnect event handlers during session dispose ([3b15ac5](https:/home/circleci/project/semantic-release-remote/commit/3b15ac5e3988b6596eb1c3d6417dca89e68a38f8))

## [2.0.1](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0...v2.0.1) (2023-05-25)


### Bug Fixes

* **mac:** even more versioning fixes ([07c035d](https:/home/circleci/project/semantic-release-remote/commit/07c035dea84a16ce02368cc1bb5f7f32ff1b3885))

# [2.0.0](https:/home/circleci/project/semantic-release-remote/compare/v1.4.0...v2.0.0) (2023-05-25)


### Bug Fixes

* always call context.completeRequest, even when not returning a value ([9cabb03](https:/home/circleci/project/semantic-release-remote/commit/9cabb03495b089cdc23fb257a0c3fea7e603c225))
* **build:** fix missing CFBundleVersion in Safari extension ([7a67ea1](https:/home/circleci/project/semantic-release-remote/commit/7a67ea18025396c26a359f435a2c1cb1ed7ab8c3))
* calculate build number from label ([2eb8015](https:/home/circleci/project/semantic-release-remote/commit/2eb8015d457263111219f2fc4a5e7d5505c6efb4))
* **ci:** speculative fix for Homebrew failing on CI ([4b12ce4](https:/home/circleci/project/semantic-release-remote/commit/4b12ce4d1501eaea92ee76c1e11fc808ccc7ad11))
* **ci:** update VS Mac installer script for 17.4 ([9221e1e](https:/home/circleci/project/semantic-release-remote/commit/9221e1e68a0c5cb8e35777c914fd9e17e954a5d7))
* **common:** make session immediately so we don't miss the first message ([d53d5c8](https:/home/circleci/project/semantic-release-remote/commit/d53d5c8a9dd02563c5e75208cfc7125386d5f85a))
* **common:** remove `EventAwaiter(EventHandler<T>, ...` ([9032a01](https:/home/circleci/project/semantic-release-remote/commit/9032a013c8b5dc967f5a53a50546499b55af6b55))
* **deps:** update dependency scratch-vm to v1.5.28 ([441b7fd](https:/home/circleci/project/semantic-release-remote/commit/441b7fdf3572b093d3b4c0c1022e5472dbcdaff9))
* **deps:** update dependency scratch-vm to v1.5.31 ([2c60027](https:/home/circleci/project/semantic-release-remote/commit/2c60027674e6cb3f0942d2c380b01b765bef12f8))
* **deps:** update dependency scratch-vm to v1.5.32 ([964a53f](https:/home/circleci/project/semantic-release-remote/commit/964a53f0ed1e594d1cf3e983c9830402ce743f05))
* **deps:** update dependency scratch-vm to v1.5.33 ([1c3a4cf](https:/home/circleci/project/semantic-release-remote/commit/1c3a4cfd0b13ca938cd53b44e26491415ea80e43))
* **deps:** update dependency scratch-vm to v1.5.34 ([b19fe2a](https:/home/circleci/project/semantic-release-remote/commit/b19fe2a64639e4ff17cd965d82965bfea6ce0603))
* **deps:** update dependency scratch-vm to v1.5.35 ([7543466](https:/home/circleci/project/semantic-release-remote/commit/7543466407de1f1f297afd148f07036bd977109b))
* **deps:** update dependency scratch-vm to v1.5.36 ([cbc0e7c](https:/home/circleci/project/semantic-release-remote/commit/cbc0e7ca8f472199acc52a2e408a878e462d0240))
* **deps:** update dependency scratch-vm to v1.5.37 ([79af6ab](https:/home/circleci/project/semantic-release-remote/commit/79af6ab191eb881c14e0403a5524c4da42e865d6))
* **deps:** update dependency scratch-vm to v1.5.38 ([f200619](https:/home/circleci/project/semantic-release-remote/commit/f2006198489bc938ce4c46ec879fe4d182ec8c5f))
* **deps:** update dependency scratch-vm to v1.5.40 ([f2b6787](https:/home/circleci/project/semantic-release-remote/commit/f2b67876984b75fca7286902be800d584959f58a))
* **deps:** update dependency scratch-vm to v1.5.41 ([5e25dba](https:/home/circleci/project/semantic-release-remote/commit/5e25dba6cceb03df542aa1e4d920326f3f0b534e))
* **deps:** update dependency scratch-vm to v1.5.42 ([7d8d1b2](https:/home/circleci/project/semantic-release-remote/commit/7d8d1b25a873866aef4cf9fe12a664ab94ada90d))
* disable BLE restore to fix 'Bluetooth unavailable' issue ([8fdc3d1](https:/home/circleci/project/semantic-release-remote/commit/8fdc3d166edb6fb49b25ed2f467a0f77227dc630))
* dispose of cbManager on session shutdown ([5423e78](https:/home/circleci/project/semantic-release-remote/commit/5423e7800ba21bdb50874d23a88d0cee64c2c54d))
* don't embed IOBluetooth.framework ([563070d](https:/home/circleci/project/semantic-release-remote/commit/563070d67e5a88cc96a196759f2d2b59b0f4706b))
* **extension:** inject project marketing version into web extension manifest ([6aa609d](https:/home/circleci/project/semantic-release-remote/commit/6aa609d100961b7f74f4345c28137393988a2835))
* fix DisposedException by removing cancellation token ([eed937f](https:/home/circleci/project/semantic-release-remote/commit/eed937fd185f58295733e63dc8879a32e5a5ee10))
* fix minor MAS compliance issues ([149076c](https:/home/circleci/project/semantic-release-remote/commit/149076c07aa6c6e725e09130ee23a397b3e6e9eb))
* generate icons directly from SVGs for better quality ([8d3b8ce](https:/home/circleci/project/semantic-release-remote/commit/8d3b8ce38a1000552d92bdce7da1cf98fbd9b134))
* implement a BT connection dance that works on macOS 10 and 12 ([159ca00](https:/home/circleci/project/semantic-release-remote/commit/159ca006789956de12e4282b2d088217eb5bb17a))
* **Mac:** add real Bluetooth permissions request messages ([39cdf3c](https:/home/circleci/project/semantic-release-remote/commit/39cdf3cd509a1c475dbc80b08d919607a6ac1f22))
* **Mac:** add real icons for Safari extension ([f081c71](https:/home/circleci/project/semantic-release-remote/commit/f081c7130d97a86f55259bd76eef4fdd51bd1856))
* **MacBLE:** allow more time for the Bluetooth state to settle ([d2c1cf9](https:/home/circleci/project/semantic-release-remote/commit/d2c1cf97845060e88a00d69a66c13580abb7c74e))
* **macBLE:** fix 'API MISUSE' log message ([b46f435](https:/home/circleci/project/semantic-release-remote/commit/b46f4359f6ed9feb8734cfbc66d9936af6303201))
* **macBLE:** handle UpdatedState even if it fires during CBCentralManager ctor ([d2df409](https:/home/circleci/project/semantic-release-remote/commit/d2df40995861311b02875c03c2f2151038e3c8e5))
* **macBT:** add 'Options' / PIN instructions to pairing dialog ([d58f5d2](https:/home/circleci/project/semantic-release-remote/commit/d58f5d243aeafb7756c987350b439b698c7eaa7d))
* **MacBT:** dispose of inquiry & channel properly ([b3c48ef](https:/home/circleci/project/semantic-release-remote/commit/b3c48ef1662a93776e68181a5e745a4b88b9670d))
* **MacBT:** make BT disconnect/reconnect more reliable, especially after pairing ([53bbe3b](https:/home/circleci/project/semantic-release-remote/commit/53bbe3b6e39fc9b27bf11119c888c4b36a39771c))
* **macBT:** poll to reliably detect RFCOMM channel open ([d42cfdb](https:/home/circleci/project/semantic-release-remote/commit/d42cfdb63751ce511f2053ff4130e2a41b99a751))
* **Mac:** correct target macOS version ([71e7a13](https:/home/circleci/project/semantic-release-remote/commit/71e7a1303397c7138604131c89bbdcf5793adc9a))
* **Mac:** embed Safari helper extension into the Scratch Link app bundle ([9c6bb30](https:/home/circleci/project/semantic-release-remote/commit/9c6bb30273b4597e1e3ddd451167cffe6231a854))
* **mac:** fix CI artifact renaming ([7a05fdd](https:/home/circleci/project/semantic-release-remote/commit/7a05fdda50fc7a498bbdc6d4068cf305177669b7))
* **Mac:** fix Safari, especially Link->Client notifications ([5bae1ea](https:/home/circleci/project/semantic-release-remote/commit/5bae1ea319dd96eed6a92074a1ba59ecdaca89ca))
* **mac:** fix tccd error message about kTCCServiceAppleEvents ([bdfc8c0](https:/home/circleci/project/semantic-release-remote/commit/bdfc8c08a6caae205e599b9cca28aedc627d1589))
* **Mac:** hide Safari extensions for non-MAS builds ([58138c5](https:/home/circleci/project/semantic-release-remote/commit/58138c5c89d17ff6d4dfd40d1bfa3ad95c88f27b))
* **Mac:** make sure GetSettledBluetoothState() doesn't miss an event ([124b6a0](https:/home/circleci/project/semantic-release-remote/commit/124b6a0cef58bd027249656ac4d183f76454d8f5))
* **Mac:** properly Dispose() of the status bar item ([4cb46b5](https:/home/circleci/project/semantic-release-remote/commit/4cb46b56588d74cd8cf54e79f36a7a6fafe53f59))
* **Mac:** remove browser_action popup ([9717935](https:/home/circleci/project/semantic-release-remote/commit/971793558fdf949622c79e28db93dd43083c8938))
* **Mac:** Safari extension improvements ([14f9f99](https:/home/circleci/project/semantic-release-remote/commit/14f9f99b8cb25e7704e53f31f6589f7205b4c66a))
* **Mac:** show Safari extension menu only if supported ([d019142](https:/home/circleci/project/semantic-release-remote/commit/d01914241789fc639def818f8553799b2915c198))
* make CI robust against VS updates ([950d3de](https:/home/circleci/project/semantic-release-remote/commit/950d3deb307226403b537874cadb1f64d2886ac6))
* make didDiscoverPeripheral a notification ([e51fa01](https:/home/circleci/project/semantic-release-remote/commit/e51fa01b799fcc2c9030a66c4bfe448f4aabbc08))
* **menu:** 'Manage Safari Extensions' => 'Manage Safari Extensions...' ([dc5c481](https:/home/circleci/project/semantic-release-remote/commit/dc5c48127842be5e3f756f077a0d1e284d1002e8))
* more BT connection tweaks ([7a1e0d0](https:/home/circleci/project/semantic-release-remote/commit/7a1e0d014a05f3af968d998c2caf888987501618))
* resolve crash on session close while connecting ([32f8981](https:/home/circleci/project/semantic-release-remote/commit/32f89814873eb19045cffcfe40a3c96f70bce54b))
* **Safari:** add timeout for initial connection ([e1c9de0](https:/home/circleci/project/semantic-release-remote/commit/e1c9de00f1dbf55c1da8bd2bd935f23015b34450))
* **Safari:** close session if Scratch Link goes away ([83f85f0](https:/home/circleci/project/semantic-release-remote/commit/83f85f028996d12e2a7d6f2b6c4f93608d60bef8))
* **safari:** don't cause Safari to steal focus for every Scratch Link -> page message ([f17184f](https:/home/circleci/project/semantic-release-remote/commit/f17184f5a1e163232a0ee76133cd2953bb382a0d))
* use semantic-release version for build ([17709dd](https:/home/circleci/project/semantic-release-remote/commit/17709dd709a59a1b4d5fa10b4a4ed50834ffd893))
* **version:** embed GitVersion info correctly and document version scheme ([6501e49](https:/home/circleci/project/semantic-release-remote/commit/6501e49073ac852e71ccd048973fb7b5a383c506))
* **webextension:** close session on client unload ([caac99e](https:/home/circleci/project/semantic-release-remote/commit/caac99e9c0fa15a940642dc5c9063dba45a40b5f))
* **webextension:** keep Safari sessions alive for longer than 5 seconds ([4981508](https:/home/circleci/project/semantic-release-remote/commit/498150869982c3d21f5463cf646e337fd789b970))
* **webextension:** limit number of outstanding poll requests ([c5137bb](https:/home/circleci/project/semantic-release-remote/commit/c5137bb7a06c1701592669196508ae9b26ee97be))
* **win:** build framework-dependent AnyCPU for further install size reduction ([b1f776c](https:/home/circleci/project/semantic-release-remote/commit/b1f776c19f07652ea09c3152325a35578f9fdcf1))
* **win:** discover both paired and unpaired BT devices ([23ff634](https:/home/circleci/project/semantic-release-remote/commit/23ff634560930041ebb66ae6476839825bb713ba))
* **win:** don't crash if BT connect fails ([522f65f](https:/home/circleci/project/semantic-release-remote/commit/522f65f199741e2e704f716952a2db8c7508640f))
* **windows:** fix *.msixupload generation ([3a1c172](https:/home/circleci/project/semantic-release-remote/commit/3a1c1727bcfbe46aa549a4c15b3b0f7e750b0527))
* **windows:** fix incorrect root namespace ([e25a604](https:/home/circleci/project/semantic-release-remote/commit/e25a604be0238ef3501447df411c7816aea31f26))
* **windows:** implement WinBLESession.Dispose ([9a0e1f7](https:/home/circleci/project/semantic-release-remote/commit/9a0e1f7ec1202ae24abc5ca988c4fa54c822bffd))
* **Win:** fix larger icon sizes being ignored sometimes ([e79252f](https:/home/circleci/project/semantic-release-remote/commit/e79252f2ddf2e15987aab8e2205a95aceaa80cb1))
* **Win:** set assembly attributes including version info ([8379c15](https:/home/circleci/project/semantic-release-remote/commit/8379c153d9b4273bf0e2814a3ebf6be3f2d3e260))
* **win:** set WindowsPackageType=None to fix debugging ([4b151e1](https:/home/circleci/project/semantic-release-remote/commit/4b151e1884915a39f059d696a968557f04e4ff7b))
* work around macOS 12 OpenRfcommChannelSync timeout ([68e7efc](https:/home/circleci/project/semantic-release-remote/commit/68e7efc069e8188dd7ee4d0b0e5deff43d7bdd14))


### chore

* clean slate for Scratch Link 2.0 ([f30cff3](https:/home/circleci/project/semantic-release-remote/commit/f30cff3e5b0fbd2fda423e8609cbd6576c45131a))


### Features

* add Windows tray icon ([29b961b](https:/home/circleci/project/semantic-release-remote/commit/29b961b8bb86070fb67012def05f195b75438086))
* **MacBT:** display pairing help when connecting to unpaired peripheral ([feb100e](https:/home/circleci/project/semantic-release-remote/commit/feb100e3c0e40ce34759246ca27b247ecbb201fc))
* **Safari:** inject client script into page if script ID is present ([9bc1ef4](https:/home/circleci/project/semantic-release-remote/commit/9bc1ef433ced60b1dc40dc68d0ffe833ce137199))
* **Win:** add proper Windows icon for app and tray ([e0e96c2](https:/home/circleci/project/semantic-release-remote/commit/e0e96c23e791eef77e136f4188a0fa621c1f0cb3))
* **win:** convert BT session for Scratch Link 2.0 ([b2bc874](https:/home/circleci/project/semantic-release-remote/commit/b2bc874b7dea108b10fe2eaa4cd8cdd42a1b4f76))
* **windows:** BLE session first draft ([224e694](https:/home/circleci/project/semantic-release-remote/commit/224e6948749997395102f2c2de2e12163627c37a))
* **windows:** build and run ScratchApp, receive WS connections ([05d2866](https:/home/circleci/project/semantic-release-remote/commit/05d2866f2bca7f3bee8af67e0769458b7c4399e9))
* **windows:** generate image assets for MSIX ([d77a006](https:/home/circleci/project/semantic-release-remote/commit/d77a0064a0cd25bac8b8b2b7e3c7d0b146ead69a))


### Performance Improvements

* **Win:** shrink tray icon, speed up svg-convert.sh ([adeaf1d](https:/home/circleci/project/semantic-release-remote/commit/adeaf1da6b1f48ce993391aa764a0acf53898f74))


### BREAKING CHANGES

* Scratch Link 2.0 will drop support for some older
versions of macOS.

# [2.0.0-develop.18](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.17...v2.0.0-develop.18) (2023-05-24)


### Bug Fixes

* **build:** fix missing CFBundleVersion in Safari extension ([7a67ea1](https:/home/circleci/project/semantic-release-remote/commit/7a67ea18025396c26a359f435a2c1cb1ed7ab8c3))

# [2.0.0-develop.17](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.16...v2.0.0-develop.17) (2023-04-29)


### Bug Fixes

* **Win:** fix larger icon sizes being ignored sometimes ([e79252f](https:/home/circleci/project/semantic-release-remote/commit/e79252f2ddf2e15987aab8e2205a95aceaa80cb1))
* **Win:** set assembly attributes including version info ([8379c15](https:/home/circleci/project/semantic-release-remote/commit/8379c153d9b4273bf0e2814a3ebf6be3f2d3e260))


### Features

* add Windows tray icon ([29b961b](https:/home/circleci/project/semantic-release-remote/commit/29b961b8bb86070fb67012def05f195b75438086))
* **Win:** add proper Windows icon for app and tray ([e0e96c2](https:/home/circleci/project/semantic-release-remote/commit/e0e96c23e791eef77e136f4188a0fa621c1f0cb3))


### Performance Improvements

* **Win:** shrink tray icon, speed up svg-convert.sh ([adeaf1d](https:/home/circleci/project/semantic-release-remote/commit/adeaf1da6b1f48ce993391aa764a0acf53898f74))

# [2.0.0-develop.16](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.15...v2.0.0-develop.16) (2023-04-24)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.42 ([7d8d1b2](https:/home/circleci/project/semantic-release-remote/commit/7d8d1b25a873866aef4cf9fe12a664ab94ada90d))

# [2.0.0-develop.15](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.14...v2.0.0-develop.15) (2023-04-22)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.41 ([5e25dba](https:/home/circleci/project/semantic-release-remote/commit/5e25dba6cceb03df542aa1e4d920326f3f0b534e))

# [2.0.0-develop.14](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.13...v2.0.0-develop.14) (2023-04-22)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.40 ([f2b6787](https:/home/circleci/project/semantic-release-remote/commit/f2b67876984b75fca7286902be800d584959f58a))

# [2.0.0-develop.13](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.12...v2.0.0-develop.13) (2023-04-21)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.38 ([f200619](https:/home/circleci/project/semantic-release-remote/commit/f2006198489bc938ce4c46ec879fe4d182ec8c5f))

# [2.0.0-develop.12](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.11...v2.0.0-develop.12) (2023-04-21)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.37 ([79af6ab](https:/home/circleci/project/semantic-release-remote/commit/79af6ab191eb881c14e0403a5524c4da42e865d6))

# [2.0.0-develop.11](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.10...v2.0.0-develop.11) (2023-04-20)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.36 ([cbc0e7c](https:/home/circleci/project/semantic-release-remote/commit/cbc0e7ca8f472199acc52a2e408a878e462d0240))

# [2.0.0-develop.10](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.9...v2.0.0-develop.10) (2023-04-19)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.35 ([7543466](https:/home/circleci/project/semantic-release-remote/commit/7543466407de1f1f297afd148f07036bd977109b))

# [2.0.0-develop.9](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.8...v2.0.0-develop.9) (2023-04-19)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.34 ([b19fe2a](https:/home/circleci/project/semantic-release-remote/commit/b19fe2a64639e4ff17cd965d82965bfea6ce0603))

# [2.0.0-develop.8](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.7...v2.0.0-develop.8) (2023-04-17)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.33 ([1c3a4cf](https:/home/circleci/project/semantic-release-remote/commit/1c3a4cfd0b13ca938cd53b44e26491415ea80e43))

# [2.0.0-develop.7](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.6...v2.0.0-develop.7) (2023-04-15)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.32 ([964a53f](https:/home/circleci/project/semantic-release-remote/commit/964a53f0ed1e594d1cf3e983c9830402ce743f05))

# [2.0.0-develop.6](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.5...v2.0.0-develop.6) (2023-04-14)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.31 ([2c60027](https:/home/circleci/project/semantic-release-remote/commit/2c60027674e6cb3f0942d2c380b01b765bef12f8))

# [2.0.0-develop.5](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.4...v2.0.0-develop.5) (2023-04-06)


### Bug Fixes

* generate icons directly from SVGs for better quality ([8d3b8ce](https:/home/circleci/project/semantic-release-remote/commit/8d3b8ce38a1000552d92bdce7da1cf98fbd9b134))
* **mac:** fix CI artifact renaming ([7a05fdd](https:/home/circleci/project/semantic-release-remote/commit/7a05fdda50fc7a498bbdc6d4068cf305177669b7))
* **win:** build framework-dependent AnyCPU for further install size reduction ([b1f776c](https:/home/circleci/project/semantic-release-remote/commit/b1f776c19f07652ea09c3152325a35578f9fdcf1))
* **win:** discover both paired and unpaired BT devices ([23ff634](https:/home/circleci/project/semantic-release-remote/commit/23ff634560930041ebb66ae6476839825bb713ba))
* **win:** don't crash if BT connect fails ([522f65f](https:/home/circleci/project/semantic-release-remote/commit/522f65f199741e2e704f716952a2db8c7508640f))
* **windows:** fix *.msixupload generation ([3a1c172](https:/home/circleci/project/semantic-release-remote/commit/3a1c1727bcfbe46aa549a4c15b3b0f7e750b0527))
* **windows:** fix incorrect root namespace ([e25a604](https:/home/circleci/project/semantic-release-remote/commit/e25a604be0238ef3501447df411c7816aea31f26))
* **windows:** implement WinBLESession.Dispose ([9a0e1f7](https:/home/circleci/project/semantic-release-remote/commit/9a0e1f7ec1202ae24abc5ca988c4fa54c822bffd))
* **win:** set WindowsPackageType=None to fix debugging ([4b151e1](https:/home/circleci/project/semantic-release-remote/commit/4b151e1884915a39f059d696a968557f04e4ff7b))


### Features

* **win:** convert BT session for Scratch Link 2.0 ([b2bc874](https:/home/circleci/project/semantic-release-remote/commit/b2bc874b7dea108b10fe2eaa4cd8cdd42a1b4f76))
* **windows:** BLE session first draft ([224e694](https:/home/circleci/project/semantic-release-remote/commit/224e6948749997395102f2c2de2e12163627c37a))
* **windows:** build and run ScratchApp, receive WS connections ([05d2866](https:/home/circleci/project/semantic-release-remote/commit/05d2866f2bca7f3bee8af67e0769458b7c4399e9))
* **windows:** generate image assets for MSIX ([d77a006](https:/home/circleci/project/semantic-release-remote/commit/d77a0064a0cd25bac8b8b2b7e3c7d0b146ead69a))

# [2.0.0-develop.4](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.3...v2.0.0-develop.4) (2023-04-06)


### Bug Fixes

* calculate build number from label ([2eb8015](https:/home/circleci/project/semantic-release-remote/commit/2eb8015d457263111219f2fc4a5e7d5505c6efb4))

# [2.0.0-develop.3](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.2...v2.0.0-develop.3) (2023-04-06)


### Bug Fixes

* **deps:** update dependency scratch-vm to v1.5.28 ([441b7fd](https:/home/circleci/project/semantic-release-remote/commit/441b7fdf3572b093d3b4c0c1022e5472dbcdaff9))

# [2.0.0-develop.2](https:/home/circleci/project/semantic-release-remote/compare/v2.0.0-develop.1...v2.0.0-develop.2) (2023-04-06)


### Bug Fixes

* use semantic-release version for build ([17709dd](https:/home/circleci/project/semantic-release-remote/commit/17709dd709a59a1b4d5fa10b4a4ed50834ffd893))

# [2.0.0-develop.1](https:/home/circleci/project/semantic-release-remote/compare/v1.4.0...v2.0.0-develop.1) (2023-04-06)


### Bug Fixes

* always call context.completeRequest, even when not returning a value ([9cabb03](https:/home/circleci/project/semantic-release-remote/commit/9cabb03495b089cdc23fb257a0c3fea7e603c225))
* **ci:** speculative fix for Homebrew failing on CI ([4b12ce4](https:/home/circleci/project/semantic-release-remote/commit/4b12ce4d1501eaea92ee76c1e11fc808ccc7ad11))
* **ci:** update VS Mac installer script for 17.4 ([9221e1e](https:/home/circleci/project/semantic-release-remote/commit/9221e1e68a0c5cb8e35777c914fd9e17e954a5d7))
* **common:** make session immediately so we don't miss the first message ([d53d5c8](https:/home/circleci/project/semantic-release-remote/commit/d53d5c8a9dd02563c5e75208cfc7125386d5f85a))
* **common:** remove `EventAwaiter(EventHandler<T>, ...` ([9032a01](https:/home/circleci/project/semantic-release-remote/commit/9032a013c8b5dc967f5a53a50546499b55af6b55))
* disable BLE restore to fix 'Bluetooth unavailable' issue ([8fdc3d1](https:/home/circleci/project/semantic-release-remote/commit/8fdc3d166edb6fb49b25ed2f467a0f77227dc630))
* dispose of cbManager on session shutdown ([5423e78](https:/home/circleci/project/semantic-release-remote/commit/5423e7800ba21bdb50874d23a88d0cee64c2c54d))
* don't embed IOBluetooth.framework ([563070d](https:/home/circleci/project/semantic-release-remote/commit/563070d67e5a88cc96a196759f2d2b59b0f4706b))
* **extension:** inject project marketing version into web extension manifest ([6aa609d](https:/home/circleci/project/semantic-release-remote/commit/6aa609d100961b7f74f4345c28137393988a2835))
* fix DisposedException by removing cancellation token ([eed937f](https:/home/circleci/project/semantic-release-remote/commit/eed937fd185f58295733e63dc8879a32e5a5ee10))
* fix minor MAS compliance issues ([149076c](https:/home/circleci/project/semantic-release-remote/commit/149076c07aa6c6e725e09130ee23a397b3e6e9eb))
* implement a BT connection dance that works on macOS 10 and 12 ([159ca00](https:/home/circleci/project/semantic-release-remote/commit/159ca006789956de12e4282b2d088217eb5bb17a))
* **Mac:** add real Bluetooth permissions request messages ([39cdf3c](https:/home/circleci/project/semantic-release-remote/commit/39cdf3cd509a1c475dbc80b08d919607a6ac1f22))
* **Mac:** add real icons for Safari extension ([f081c71](https:/home/circleci/project/semantic-release-remote/commit/f081c7130d97a86f55259bd76eef4fdd51bd1856))
* **MacBLE:** allow more time for the Bluetooth state to settle ([d2c1cf9](https:/home/circleci/project/semantic-release-remote/commit/d2c1cf97845060e88a00d69a66c13580abb7c74e))
* **macBLE:** fix 'API MISUSE' log message ([b46f435](https:/home/circleci/project/semantic-release-remote/commit/b46f4359f6ed9feb8734cfbc66d9936af6303201))
* **macBLE:** handle UpdatedState even if it fires during CBCentralManager ctor ([d2df409](https:/home/circleci/project/semantic-release-remote/commit/d2df40995861311b02875c03c2f2151038e3c8e5))
* **macBT:** add 'Options' / PIN instructions to pairing dialog ([d58f5d2](https:/home/circleci/project/semantic-release-remote/commit/d58f5d243aeafb7756c987350b439b698c7eaa7d))
* **MacBT:** dispose of inquiry & channel properly ([b3c48ef](https:/home/circleci/project/semantic-release-remote/commit/b3c48ef1662a93776e68181a5e745a4b88b9670d))
* **MacBT:** make BT disconnect/reconnect more reliable, especially after pairing ([53bbe3b](https:/home/circleci/project/semantic-release-remote/commit/53bbe3b6e39fc9b27bf11119c888c4b36a39771c))
* **macBT:** poll to reliably detect RFCOMM channel open ([d42cfdb](https:/home/circleci/project/semantic-release-remote/commit/d42cfdb63751ce511f2053ff4130e2a41b99a751))
* **Mac:** correct target macOS version ([71e7a13](https:/home/circleci/project/semantic-release-remote/commit/71e7a1303397c7138604131c89bbdcf5793adc9a))
* **Mac:** embed Safari helper extension into the Scratch Link app bundle ([9c6bb30](https:/home/circleci/project/semantic-release-remote/commit/9c6bb30273b4597e1e3ddd451167cffe6231a854))
* **Mac:** fix Safari, especially Link->Client notifications ([5bae1ea](https:/home/circleci/project/semantic-release-remote/commit/5bae1ea319dd96eed6a92074a1ba59ecdaca89ca))
* **mac:** fix tccd error message about kTCCServiceAppleEvents ([bdfc8c0](https:/home/circleci/project/semantic-release-remote/commit/bdfc8c08a6caae205e599b9cca28aedc627d1589))
* **Mac:** hide Safari extensions for non-MAS builds ([58138c5](https:/home/circleci/project/semantic-release-remote/commit/58138c5c89d17ff6d4dfd40d1bfa3ad95c88f27b))
* **Mac:** make sure GetSettledBluetoothState() doesn't miss an event ([124b6a0](https:/home/circleci/project/semantic-release-remote/commit/124b6a0cef58bd027249656ac4d183f76454d8f5))
* **Mac:** properly Dispose() of the status bar item ([4cb46b5](https:/home/circleci/project/semantic-release-remote/commit/4cb46b56588d74cd8cf54e79f36a7a6fafe53f59))
* **Mac:** remove browser_action popup ([9717935](https:/home/circleci/project/semantic-release-remote/commit/971793558fdf949622c79e28db93dd43083c8938))
* **Mac:** Safari extension improvements ([14f9f99](https:/home/circleci/project/semantic-release-remote/commit/14f9f99b8cb25e7704e53f31f6589f7205b4c66a))
* **Mac:** show Safari extension menu only if supported ([d019142](https:/home/circleci/project/semantic-release-remote/commit/d01914241789fc639def818f8553799b2915c198))
* make CI robust against VS updates ([950d3de](https:/home/circleci/project/semantic-release-remote/commit/950d3deb307226403b537874cadb1f64d2886ac6))
* make didDiscoverPeripheral a notification ([e51fa01](https:/home/circleci/project/semantic-release-remote/commit/e51fa01b799fcc2c9030a66c4bfe448f4aabbc08))
* **menu:** 'Manage Safari Extensions' => 'Manage Safari Extensions...' ([dc5c481](https:/home/circleci/project/semantic-release-remote/commit/dc5c48127842be5e3f756f077a0d1e284d1002e8))
* more BT connection tweaks ([7a1e0d0](https:/home/circleci/project/semantic-release-remote/commit/7a1e0d014a05f3af968d998c2caf888987501618))
* resolve crash on session close while connecting ([32f8981](https:/home/circleci/project/semantic-release-remote/commit/32f89814873eb19045cffcfe40a3c96f70bce54b))
* **Safari:** add timeout for initial connection ([e1c9de0](https:/home/circleci/project/semantic-release-remote/commit/e1c9de00f1dbf55c1da8bd2bd935f23015b34450))
* **Safari:** close session if Scratch Link goes away ([83f85f0](https:/home/circleci/project/semantic-release-remote/commit/83f85f028996d12e2a7d6f2b6c4f93608d60bef8))
* **safari:** don't cause Safari to steal focus for every Scratch Link -> page message ([f17184f](https:/home/circleci/project/semantic-release-remote/commit/f17184f5a1e163232a0ee76133cd2953bb382a0d))
* **version:** embed GitVersion info correctly and document version scheme ([6501e49](https:/home/circleci/project/semantic-release-remote/commit/6501e49073ac852e71ccd048973fb7b5a383c506))
* **webextension:** close session on client unload ([caac99e](https:/home/circleci/project/semantic-release-remote/commit/caac99e9c0fa15a940642dc5c9063dba45a40b5f))
* **webextension:** keep Safari sessions alive for longer than 5 seconds ([4981508](https:/home/circleci/project/semantic-release-remote/commit/498150869982c3d21f5463cf646e337fd789b970))
* **webextension:** limit number of outstanding poll requests ([c5137bb](https:/home/circleci/project/semantic-release-remote/commit/c5137bb7a06c1701592669196508ae9b26ee97be))
* work around macOS 12 OpenRfcommChannelSync timeout ([68e7efc](https:/home/circleci/project/semantic-release-remote/commit/68e7efc069e8188dd7ee4d0b0e5deff43d7bdd14))


### chore

* clean slate for Scratch Link 2.0 ([f30cff3](https:/home/circleci/project/semantic-release-remote/commit/f30cff3e5b0fbd2fda423e8609cbd6576c45131a))


### Features

* **MacBT:** display pairing help when connecting to unpaired peripheral ([feb100e](https:/home/circleci/project/semantic-release-remote/commit/feb100e3c0e40ce34759246ca27b247ecbb201fc))
* **Safari:** inject client script into page if script ID is present ([9bc1ef4](https:/home/circleci/project/semantic-release-remote/commit/9bc1ef433ced60b1dc40dc68d0ffe833ce137199))


### BREAKING CHANGES

* Scratch Link 2.0 will drop support for some older
versions of macOS.
