﻿using System;
using System.Collections.Generic;
using WebService.Models;

namespace WebService.Services
{
    public class NotesService : INotesService
    {
        private readonly INotesRepository _repository;

        public NotesService(INotesRepository repository)
        {
            if (repository == null)
            {
                throw new ArgumentNullException("repository");
            }

            _repository = repository;
        }

        public bool DoesItemExist(int id)
        {
            //if (id == 0)
            //{
            //    throw new ArgumentNullException(id);
            //}

            return _repository.DoesItemExist(id);
        }

        public NoteModel Find(int id)
        {
            //if (string.IsNullOrWhiteSpace(id))
            //{
            //    throw new ArgumentNullException("id");
            //}

            return _repository.Find(id);
        }

        public IEnumerable<NoteModel> GetData()
        {
            return _repository.All();
        }

        public void InsertData(NoteModel item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            _repository.Insert(item);
        }

        public void UpdateData(NoteModel item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }


            _repository.Update(item);
        }

        public void DeleteData(int id)
        {
            //if (string.IsNullOrWhiteSpace(id))
            //{
            //    throw new ArgumentNullException("id");
            //}

            _repository.Delete(id);
        }
    }
}