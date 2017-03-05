﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NoteBook.Contracts;
using NoteBook.Helpers;
using NoteBook.Models;

namespace NoteBook.Services
{
    public class RemoteNotesService : INotesService 
    {
        public INotesService NotesService { get; private set; }

        public RemoteNotesService(INotesService notesService)
        {
            NotesService = notesService;
        }

        public async Task<IEnumerable<NoteModel>> GetAllNotes()
        {
            var items = new List<NoteModel>();
            HttpResponseMessage response;

            using (var client = AuthHelper.GetAuthHttpClient())
            {
                response = client.GetAsync(Settings.Url + Settings.NoteGetAllNotesPath).Result;
            }

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                try
                {
                    items = JsonConvert.DeserializeObject<List<NoteModel>>(content);
                }
                catch (Exception)
                {
                    throw new InvalidCastException("Cannot deserialize list notes");
                }
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HttpRequestException("Not authorized");
            }

            return items;
        }

        public Task<IEnumerable<NoteModel>> GetSyncNotes(DateTime time)
        {
            var syncModel = new SyncModel {LastModify = UserSettings.SyncDate };
            var notes = NotesService.GetSyncNotes(DateTime.Parse(UserSettings.SyncDate)).Result.ToList() ?? new List<NoteModel>();

            syncModel.NoteModels = notes;

            HttpResponseMessage response;

            var json = JsonConvert.SerializeObject(syncModel);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            using (var client = AuthHelper.GetAuthHttpClient())
            {
                response = client.PostAsync(Settings.Url + Settings.NoteSyncPath, content).Result;
            }

            if (response.IsSuccessStatusCode)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    if (notes[i].ImageInBytes != null && notes[i].ImageInBytes.Length > 0)
                    {
                        UploadBytes(notes[i]);
                        notes[i].ImageInBytes = null;
                    }
                }
                try
                {
                    var result = response.Content.ReadAsStringAsync().Result;
                    var syncModelParse = JsonConvert.DeserializeObject<SyncModel>(result);
                    UserSettings.SyncDate = syncModelParse.LastModify;

                    var items = syncModelParse.NoteModels;

                    foreach (var note in notes)
                    {
                        if (note.Delete != null)
                        {
                            NotesService.DeleteNote(note);
                        }
                    }

                    var localStorage = NotesService.GetAllNotes().Result.ToList();

                    foreach (var item in items)
                    {
                        if (localStorage.Find(x => x.NoteId == item.NoteId) == null)
                        {
                            if (item.Delete == null)
                            {
                                NotesService.CreateNote(item);
                            }
                        }
                        else
                        {
                            if (item.Delete != null)
                            {
                                NotesService.DeleteNote(item);
                            }
                            else
                            {
                                NotesService.UpdateNote(item);
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    throw new InvalidCastException("Cannot deserialize list notes");
                }
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HttpRequestException("Not authorized");
            }

            return NotesService.GetAllNotes();
        }

        public async Task<bool> CreateNote(NoteModel credentials)
        {
            credentials.NoteId = Guid.NewGuid().ToString();
            HttpResponseMessage response;

            var json = JsonConvert.SerializeObject(credentials);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using (var client = AuthHelper.GetAuthHttpClient())
            {
                response = await client.PostAsync(Settings.Url + Settings.NoteCreatePath, content);
            }

            if (response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync();

                NoteModel tempModel;
                try
                {
                    tempModel = JsonConvert.DeserializeObject<NoteModel>(text);
                }
                catch (Exception)
                {
                    throw new InvalidCastException("Cannot deserialize note");
                }

                if (credentials.MediaFile != null)
                {
                    tempModel.MediaFile = credentials.MediaFile;
                    if (! Upload(tempModel))
                        throw new HttpRequestException("Cannot upload image");
                }
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HttpRequestException("Not authorized");
            }


            return response.IsSuccessStatusCode;
        }

        public async Task<bool> UpdateNote(NoteModel credentials)
        {
            HttpResponseMessage response;

            var json = JsonConvert.SerializeObject(credentials);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using (var client = AuthHelper.GetAuthHttpClient())
            {
                response = await client.PutAsync(Settings.Url + Settings.NoteUpdatePath, content);
            }

            if (response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync();
                NoteModel tempModel;

                try
                {
                    tempModel = JsonConvert.DeserializeObject<NoteModel>(text);
                }
                catch (Exception)
                {
                    throw new InvalidCastException("Cannot deserialize note");
                }

                if (credentials.MediaFile != null)
                {
                    tempModel.MediaFile = credentials.MediaFile;
                    if (!Upload(tempModel))
                        throw new HttpRequestException("Cannot upload image");
                }
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HttpRequestException("Not authorized");
            }

            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteNote(NoteModel credentials)
        {
            HttpResponseMessage response;

            var json = JsonConvert.SerializeObject(credentials);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using (var client = AuthHelper.GetAuthHttpClient())
            {
                response = await client.PostAsync(Settings.Url + Settings.NoteDeletePath, content);
            }

            if (response.IsSuccessStatusCode)
            {
                await NotesService.DeleteNote(credentials);
            }
            else if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new HttpRequestException("Not authorized");
            }

            return response.IsSuccessStatusCode;
        }

        #region UploadingImage

        private bool Upload(NoteModel model)
        {
            HttpResponseMessage response;

            byte[] data = StreamHelper.ReadFully(model.MediaFile.Source);

            var imageStream = new ByteArrayContent(data);

            var content = new MultipartFormDataContent();
            content.Add(imageStream);

            using (var client = AuthHelper.GetAuthHttpClient())
            {
                response = client.PostAsync(Settings.Url + Settings.NoteAddImagePath + "?noteId=" + model.NoteId, content).Result;
            }

            return response.IsSuccessStatusCode;
        }

        private bool UploadBytes(NoteModel model)
        {
            HttpResponseMessage response;
            var imageStream = new ByteArrayContent(model.ImageInBytes);

            var content = new MultipartFormDataContent();
            content.Add(imageStream);

            using (var client = AuthHelper.GetAuthHttpClient())
            {
                response = client.PostAsync(Settings.Url + Settings.NoteAddImagePath + "?noteId=" + model.NoteId, content).Result;
            }

            return response.IsSuccessStatusCode;
        }

        #endregion
    }
}
