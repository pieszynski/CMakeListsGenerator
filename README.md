# CMakeListsGenerator
Generowanie plików CMakeLists.txt w VisualStudio 2015

### Użycie
Klikamy prawym na rozwiązaniu (`Solution`) i wybieramy opcję `Make CMakeLists.txt`

### Już istniejące rozwiązanie w CMake
Jak dodać nowy projekt do rozwiązania

* Dodajemy nowy projekt w katalogu rozwiązania tak aby zawierał swój podkatalog projektowy
* Dodajemy pliki `*.h` i obowiązkowo `*.cpp` (inaczej CMake nie rozpozna typu projektu)
* W istniejących projektach ustawiamy, że zależą od nowego projektu - prawym na projekcie i `Build Dependencies -> Project Dependencies...` a następnie zaznaczamy nowy projekt jako zależny
* Uruchamiamy tworzenie nowych plików opcją `Make CMakeLists.txt` w rozwiązaniu
* Budujemy projekt `ALL_BUILD`, który buduje `ZERO_CHECK` czyli projekt odświeżający strukturę projektu na podstawie plików `CMakeLists.txt`
* Dla pewności restartujemy Visual Studio bo nie jest ono odświeżyć nowej struktury (tylko przy dodawaniu nowego projektu)
* Po zrestartowaniu VisualStudio widać nową strukturę

### Odtworzenie na podstawie struktury podstawowej
Uruchamiamy poniższe polecenie w katalogu rozwiązania

Windows

	\> cmake .\

Linux

	$ cmake .

Należy pamiętać aby przestawić później projekt domyślny w VisualStudio
