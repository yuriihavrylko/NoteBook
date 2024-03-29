﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Hosting;
using System.Web.Http;
using WebService.Models;
using WebService.Repositories;
using WebService.Contracts;

namespace WebService.Controllers
{
    [Authorize]
    [RoutePrefix("api/Notes")]
    public class NotesController : BaseApiController
    {
        private INotesRepository NotesRepository;
        private IAccountRepository AccountRepository;

        public NotesController()
        {
            NotesRepository = new NotesRepository();
            AccountRepository = new AccountRepository();
        }

        public NotesController(INotesRepository notesRepository, IAccountRepository accountRepository)
        {
            NotesRepository = notesRepository;
            AccountRepository = accountRepository;
        }

        [HttpGet]
        [Route("GetAllNotes")]
        public HttpResponseMessage GetAllNotes()
        {
            var notes = NotesRepository.All(AccountRepository.GetIdByUserName(User.Identity.Name)) ??
                        new List<NoteModel>();
            var response = Request.CreateResponse(HttpStatusCode.OK, notes);

            return response;
        }


        [HttpPost]
        [Route("CreateNote")]
        public HttpResponseMessage CreateNote(NoteModel item)
        {
            HttpResponseMessage result;

            try
            {
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.NoteName) ||
                    string.IsNullOrWhiteSpace(item.NoteText))
                {
                    result = Request.CreateResponse(HttpStatusCode.BadRequest);
                }
                else
                {
                    var itemExists = NotesRepository.DoesItemExist(
                        AccountRepository.GetIdByUserName(User.Identity.Name), item.NoteId);
                    if (itemExists)
                    {
                        result = Request.CreateResponse(HttpStatusCode.BadRequest);
                    }
                    else
                    {
                        NotesRepository.Insert(AccountRepository.GetIdByUserName(User.Identity.Name), item);
                        var note = NotesRepository.Find(AccountRepository.GetIdByUserName(User.Identity.Name),
                            item.NoteId);
                        result = Request.CreateResponse(HttpStatusCode.OK, note);
                        
                    }
                }
            }
            catch (Exception e)
            {
                Debug.Print(e.Message);
                result = Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            return result;
        }

        [HttpPut]
        [Route("UpdateNote")]
        public HttpResponseMessage UpdateNote(NoteModel item)
        {
            HttpResponseMessage result;

            try
            {
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.NoteName) ||
                    string.IsNullOrWhiteSpace(item.NoteText))
                {
                    result = BuildErrorResult(HttpStatusCode.BadRequest,
                        ErrorCode.NotesItemNameAndNotesRequired.ToString());
                }
                else
                {

                    var todoItem = NotesRepository.Find(AccountRepository.GetIdByUserName(User.Identity.Name),
                        item.NoteId);
                    if (todoItem != null)
                    {
                        NotesRepository.Update(AccountRepository.GetIdByUserName(User.Identity.Name), item);
                        var note = NotesRepository.Find(AccountRepository.GetIdByUserName(User.Identity.Name),
                            item.NoteId);
                        result = BuildSuccessResult(HttpStatusCode.OK, note);
                    }
                    else
                    {
                        result = BuildErrorResult(HttpStatusCode.NotFound, ErrorCode.RecordNotFound.ToString());
                    }
                }
            }
            catch (Exception)
            {
                result = BuildErrorResult(HttpStatusCode.BadRequest, ErrorCode.CouldNotUpdateItem.ToString());
            }

            return result;
        }

        [HttpPost]
        [Route("DeleteNote")]
        public HttpResponseMessage DeleteNote(NoteModel item)
        {
            HttpResponseMessage result;

            try
            {
                var todoItem = NotesRepository.Find(AccountRepository.GetIdByUserName(User.Identity.Name), item.NoteId);
                if (todoItem != null)
                {
                    NotesRepository.Delete(AccountRepository.GetIdByUserName(User.Identity.Name), item.NoteId);
                    result = BuildSuccessResult(HttpStatusCode.OK);
                }
                else
                {
                    result = BuildErrorResult(HttpStatusCode.NotFound, ErrorCode.RecordNotFound.ToString());
                }
            }
            catch (Exception)
            {
                result = BuildErrorResult(HttpStatusCode.BadRequest, ErrorCode.CouldNotDeleteItem.ToString());
            }

            return result;
        }

        [HttpPost]
        [Route("GetSyncNotes")]
        public HttpResponseMessage GetAsyncNotes(SyncModel model)
        {
            SyncModel syncModel = new SyncModel();
            var notes =
                NotesRepository.HasChanges(AccountRepository.GetIdByUserName(User.Identity.Name), model).ToList();
            syncModel.NoteModels = notes;
            syncModel.LastModify = DateTime.Now.ToString("G");
            var response = Request.CreateResponse(HttpStatusCode.OK, syncModel);

            return response;
        }

        [HttpPost]
        [Route("PostImage")]
        public async Task<HttpResponseMessage> PostImage(string noteId)
        {
            var result = new HttpResponseMessage(HttpStatusCode.OK);

            var streamProvider = await Request.Content.ReadAsMultipartAsync();
            foreach (var file in streamProvider.Contents)
            {
                var imageStream = await file.ReadAsStreamAsync();
                string fileName;
                using (Image image = Image.FromStream(imageStream))
                {
                    string filePath = HostingEnvironment.MapPath("~/Userimage/");
                    fileName = DateTime.Now.ToFileTime() + ".png";
                    string fullPath = Path.Combine(filePath, fileName);
                    image.Save(fullPath);
                }
                NotesRepository.SetImage(noteId, Settings.Url + "Userimage/" + fileName);
            }
            return result;
        }
    }

}



