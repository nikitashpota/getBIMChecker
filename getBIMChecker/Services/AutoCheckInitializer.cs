using System;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Events;
using getBIMChecker.Services;

namespace getBIMChecker
{
    /// <summary>
    /// Статический инициализатор для интеграции автоматической проверки осей
    /// в другие проекты, которые используют getBIMChecker как библиотеку
    /// </summary>
    public static class AutoCheckInitializer
    {
        private static AutomaticCheckService _automaticCheckService;
        private static bool _isInitialized = false;

        /// <summary>
        /// Инициализировать автоматическую проверку осей.
        /// Вызывать из OnStartup() главного приложения.
        /// </summary>
        /// <param name="application">Revit Application</param>
        public static void Initialize(Application application)
        {
            if (_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("[AutoCheckInitializer] ⚠ Уже инициализировано, пропускаем");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("\n=========================================");
                System.Diagnostics.Debug.WriteLine("=== getBIMChecker - Инициализация автопроверки ===");
                System.Diagnostics.Debug.WriteLine("=========================================\n");

                // Создаем сервис автоматической проверки
                _automaticCheckService = new AutomaticCheckService();
                System.Diagnostics.Debug.WriteLine("[AutoCheckInit] ✓ AutomaticCheckService создан");

                // Подписываемся на событие синхронизации
                application.DocumentSynchronizedWithCentral +=
                    _automaticCheckService.OnDocumentSynchronizedWithCentral;

                System.Diagnostics.Debug.WriteLine("[AutoCheckInit] ✓ Подписка на DocumentSynchronizedWithCentral выполнена");
                System.Diagnostics.Debug.WriteLine("[AutoCheckInit] ✓ Автоматическая проверка осей активирована");
                System.Diagnostics.Debug.WriteLine("\n=========================================\n");

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n[AutoCheckInit] ✗✗✗ ОШИБКА ИНИЦИАЛИЗАЦИИ:");
                System.Diagnostics.Debug.WriteLine($"[AutoCheckInit] {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[AutoCheckInit] StackTrace:\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// Деинициализировать автоматическую проверку осей.
        /// Вызывать из OnShutdown() главного приложения.
        /// </summary>
        /// <param name="application">Revit Application</param>
        public static void Shutdown(Application application)
        {
            if (!_isInitialized)
            {
                System.Diagnostics.Debug.WriteLine("[AutoCheckInitializer] ⚠ Не было инициализировано, пропускаем shutdown");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("\n=========================================");
                System.Diagnostics.Debug.WriteLine("=== getBIMChecker - Завершение автопроверки ===");
                System.Diagnostics.Debug.WriteLine("=========================================\n");

                // Отписываемся от события
                if (_automaticCheckService != null)
                {
                    application.DocumentSynchronizedWithCentral -=
                        _automaticCheckService.OnDocumentSynchronizedWithCentral;

                    System.Diagnostics.Debug.WriteLine("[AutoCheckInit] ✓ Отписка от DocumentSynchronizedWithCentral выполнена");
                }

                _automaticCheckService = null;
                _isInitialized = false;

                System.Diagnostics.Debug.WriteLine("[AutoCheckInit] ✓ Автоматическая проверка осей деактивирована");
                System.Diagnostics.Debug.WriteLine("\n=========================================\n");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"\n[AutoCheckInit] ✗ ОШИБКА ЗАВЕРШЕНИЯ:");
                System.Diagnostics.Debug.WriteLine($"[AutoCheckInit] {ex.Message}");
            }
        }

        /// <summary>
        /// Проверка статуса инициализации
        /// </summary>
        public static bool IsInitialized => _isInitialized;
    }
}
