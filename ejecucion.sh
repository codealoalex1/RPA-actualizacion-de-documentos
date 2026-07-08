# 1. Aseguramos que exista la subcarpeta x64 en la ruta exacta de net10.0
mkdir -p /workspaces/RPA-actualizacion-de-documentos/3_Aplicacion/Rpa.ServicioWindows/bin/Debug/net10.0/x64

# 2. Creamos el enlace simbólico dentro de esa carpeta x64 con el nombre exacto
ln -sf /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /workspaces/RPA-actualizacion-de-documentos/3_Aplicacion/Rpa.ServicioWindows/bin/Debug/net10.0/x64/libtesseract55.dll.so

# 3. Por si acaso, creamos también el de Leptonica en esa misma subcarpeta x64 de TesseractOCR
ln -sf /usr/lib/x86_64-linux-gnu/liblept.so.5.0.4 /workspaces/RPA-actualizacion-de-documentos/3_Aplicacion/Rpa.ServicioWindows/bin/Debug/net10.0/x64/libleptonica.dll.so 2>/dev/null || true

# 4. Refrescamos los mapeos del sistema operativo
sudo ldconfig