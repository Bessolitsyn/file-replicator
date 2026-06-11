using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

[assembly: InternalsVisibleTo("FileReplicatorTests")]

namespace FileReplicator
{
    /// <summary>
    /// The main orchestrator for the file replication service. 
    /// Manages multiple synchronization tasks and coordinates between configuration and execution.
    /// <para>Основной оркестратор службы репликации файлов. 
    /// Управляет несколькими задачами синхронизации и координирует работу между конфигурацией и выполнением.</para>
    /// </summary>
    public class Replicator(Settings settings, ILogger logger ) :IDisposable
    {
        private readonly Settings _settings = settings;
        private readonly ILogger _logger = logger;
        private readonly List<FolderSync> _syncExecuters = [];
        private int _status = 0;
        private bool _isReadyToObserve = false;
        private bool _isReadyToStart = false;

        /// <summary>
        /// Current status of the replicator.
        /// 0 - stopped, 1 - started.
        /// <para>Текущий статус репликатора. 0 - остановлен, 1 - запущен.</para>
        /// </summary>
        public int Status { get=>_status; }

        /// <summary>
        /// Starts the replication process, including the observation of folders if configured.
        /// <para>Запускает процесс репликации, включая наблюдение за папками, если это настроено.</para>
        /// </summary>
        public void Start()
        {
            if (!_isReadyToObserve) InitToObserve();
            _syncExecuters.ForEach(x => { x.StartObserving(); });
            _status = 1;
        }

        /// <summary>
        /// Asynchronously stops the replication process and stops all active observers.
        /// <para>Асинхронно останавливает процесс репликации и всех активных наблюдателей.</para>
        /// </summary>
        /// <returns>A task representing the asynchronous operation. / Задача, представляющая асинхронную операцию.</returns>
        public async Task StopAsync()
        {            
            foreach (var sync in _syncExecuters)
            { 
                await sync.StopObservingAsync();
            }
            _status = 0;
            
        }
        
        /// <summary>
        /// Forces an immediate replication of all configured folders.
        /// <para>Принудительно выполняет немедленную репликацию всех настроенных папок.</para>
        /// </summary>
        /// <returns>A task representing the asynchronous operation. / Задача, представляющая асинхронную операцию.</returns>
        public async Task ExecuteAsync()
        {
            
            if (!_isReadyToStart) InitToStart();
            _logger.Log(LogLevel.Information, "==| Reptor is running");
            foreach (var item in _syncExecuters)
            {
                var tstart = DateTime.Now;
                _logger.Log(LogLevel.Information, $"==| Folders item<{item.ToString()}> is processing");
                await item.ReplicateAsync();
                var dur = DateTime.Now - tstart;
                _logger.Log(LogLevel.Information, $"==| Folders item has finished with duration: {dur.Minutes}min{dur.Seconds}sec");

            }
            _logger.Log(LogLevel.Information, "==| Reptor has finished");
        }

        /// <summary>
        /// Initializes synchronization executors for a one-time replication run.
        /// <para>Инициализирует исполнителей синхронизации для однократного запуска репликации.</para>
        /// </summary>
        private void InitToStart()
        {
            foreach (var item in _settings.SourceFolders)
            {
                var fc = new FolderSync(_logger);
                fc.SerFolderToSync(new DirectoryInfo(item.OriginalPath), new DirectoryInfo(item.DestinationPath));                
                //fc.SerFolderToSync(new DirectoryInfo("c:\\temp\\ReptorTests\\Version7"), new DirectoryInfo(item.DestinationPath));
                _syncExecuters.Add(fc);
            }
            _isReadyToStart= true;
        }

        /// <summary>
        /// Initializes synchronization executors with folder observation capabilities.
        /// <para>Инициализирует исполнителей синхронизации с возможностями наблюдения за папками.</para>
        /// </summary>
        private void InitToObserve()
        {
            foreach (var item in _settings.SourceFolders)
            {
                var fc = new FolderSync(_logger);
                fc.SetFolderToObserve(new DirectoryInfo(item.OriginalPath), new DirectoryInfo(item.DestinationPath));
                _syncExecuters.Add(fc);
            }
            _isReadyToObserve = true;
        }

        /// <summary>
        /// Releases resources used by the Replicator.
        /// <para>Освобождает ресурсы, используемые репликатором.</para>
        /// </summary>
        public void Dispose()
        {
            //throw new NotImplementedException();
        }
    }
}
