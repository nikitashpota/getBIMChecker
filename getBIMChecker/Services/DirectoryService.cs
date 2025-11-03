using System;
using System.Collections.Generic;
using System.Linq;
using getBIMChecker.Models;

namespace getBIMChecker.Services
{
    /// <summary>
    /// Сервис для работы с директориями проектов
    /// </summary>
    public class DirectoryService
    {
        private readonly DatabaseService _databaseService;

        public DirectoryService(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        /// <summary>
        /// Получить все директории с отметкой привязки текущей модели
        /// </summary>
        /// <param name="boundDirectoryId">ID директории, к которой привязана текущая модель</param>
        public List<Directory> GetAllDirectoriesWithBinding(int? boundDirectoryId)
        {
            var directories = _databaseService.GetAllDirectories();

            // Отмечаем привязанную директорию
            if (boundDirectoryId.HasValue)
            {
                foreach (var dir in directories)
                {
                    dir.IsBound = (dir.Id == boundDirectoryId.Value);
                }

                // Сортируем: привязанная директория первой
                directories = directories
                    .OrderByDescending(d => d.IsBound)
                    .ThenByDescending(d => d.CreatedAt)
                    .ToList();
            }

            return directories;
        }

        /// <summary>
        /// Создать новую директорию с валидацией
        /// </summary>
        public int CreateDirectory(string code)
        {
            // Валидация кода
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("Код директории не может быть пустым");
            }

            code = code.Trim();

            if (code.Length > 255)
            {
                throw new ArgumentException("Код директории не может быть длиннее 255 символов");
            }

            return _databaseService.CreateDirectory(code);
        }

        /// <summary>
        /// Обновить код директории с валидацией
        /// </summary>
        public void UpdateDirectory(int directoryId, string newCode)
        {
            // Валидация кода
            if (string.IsNullOrWhiteSpace(newCode))
            {
                throw new ArgumentException("Код директории не может быть пустым");
            }

            newCode = newCode.Trim();

            if (newCode.Length > 255)
            {
                throw new ArgumentException("Код директории не может быть длиннее 255 символов");
            }

            _databaseService.UpdateDirectory(directoryId, newCode);
        }

        /// <summary>
        /// Удалить директорию с подтверждением
        /// </summary>
        public bool DeleteDirectory(int directoryId, string directoryCode)
        {
            var result = System.Windows.MessageBox.Show(
                $"Вы уверены, что хотите удалить директорию '{directoryCode}'?\n\n" +
                "Будут удалены все связанные модели и оси!",
                "Подтверждение удаления",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                _databaseService.DeleteDirectory(directoryId);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Проверить существование директории по коду
        /// </summary>
        public bool DirectoryExists(string code)
        {
            var directories = _databaseService.GetAllDirectories();
            return directories.Any(d => d.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Получить директорию по ID
        /// </summary>
        public Directory GetDirectoryById(int directoryId)
        {
            var directories = _databaseService.GetAllDirectories();
            return directories.FirstOrDefault(d => d.Id == directoryId);
        }

        /// <summary>
        /// Получить директорию по коду
        /// </summary>
        public Directory GetDirectoryByCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            var directories = _databaseService.GetAllDirectories();
            return directories.FirstOrDefault(d => d.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
        }
    }
}