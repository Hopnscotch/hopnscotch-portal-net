﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using Hopnscotch.Portal.Contracts;
using Hopnscotch.Portal.Integration.AmoCRM;
using Hopnscotch.Portal.Integration.AmoCRM.DataProvider;
using Hopnscotch.Portal.Integration.AmoCRM.Entities;
using Hopnscotch.Portal.Model;

namespace Hopnscotch.Portal.Import
{
    public sealed class AmoCrmImportManager : IAmoCrmImportManager
    {
        private const int LowLevelTotalHoursNumber = 100;
        private readonly HashSet<string> lowLevelSet = new HashSet<string>
        {
            "Beginner",
            "Elementary",
            "Pre-Intermediate"
        };

        private const int HighLevelTotalHoursNumber = 120;
        private readonly HashSet<string> highLevelSet = new HashSet<string>
        {
            "Intermediate",
            "Upper Intermediate",
            "Advanced",
            "Proficiency",
            "Mover"
        };
        
        private IAmoDataProvider amoDataProvider;
        private readonly IAttendanceUow attendanceUow;
        private readonly IAmoCrmEntityConverter entityConverter;

        public AmoCrmImportManager(IAmoDataProvider amoDataProvider, IAttendanceUow attendanceUow, IAmoCrmEntityConverter entityConverter)
        {
            this.amoDataProvider = amoDataProvider;
            this.attendanceUow = attendanceUow;
            this.entityConverter = entityConverter;
        }

        public AmoCrmImportResult Import(AmoCrmImportOptions options)
        {
            if (options.SimulateImport)
            {
                amoDataProvider = new SimulationImportDataProvider(attendanceUow);
            }

            amoDataProvider.SaveDataDuringImport = options.SaveImportData;

            if (!amoDataProvider.Authenticate())
            {
                return new AmoCrmImportResult(new []
                {
                    new AmoCrmImportResultError
                    {
                        EntityType = AmoCrmEntityTypes.None,
                        Message = "Could not authenticate in amoCRM API"
                    }
                });
            }

            if (options.StartFromScratch)
            {
                ClearExistingAttendanceData();
            }

            AmoCrmImportContext context;
            try
            {
                context = new AmoCrmImportContext(amoDataProvider, entityConverter, !options.IncludeHistoricalData);
            }
            catch (ImportSimulationException e)
            {
                return new AmoCrmImportResult(new[]
                {
                    new AmoCrmImportResultError
                    {
                        EntityType = AmoCrmEntityTypes.None,
                        Message = e.Message
                    }
                });
            }
            
            ImportUsers(context);
            ImportLevels(context);
            ImportLeadStatuses(context);
            SetupContactLeadLinks(context);
            ImportContacts(context);
            ImportLeads(context);

            attendanceUow.Commit();

            return new AmoCrmImportResult();
        }

        private void ImportLeads(AmoCrmImportContext context)
        {
            foreach (var lead in context.LeadsMap.Values)
            {
                // set responsible user
                User user;
                if (context.UsersMap.TryGetValue(lead.AmoResponsibleUserId, out user))
                {
                    lead.ResponsibleUser = user;
                }

                // set group level if set and exists
                if (lead.AmoLevelId.HasValue)
                {
                    var level = attendanceUow.Levels.GetByAmoId(lead.AmoLevelId.Value);

                    // if level is not found in database, this is probably the first import, so get level from import context
                    // (already parsed but not committed to database yet)
                    if (level == null)
                    {
                        context.LevelsMap.TryGetValue(lead.AmoLevelId.Value, out level);
                    }

                    lead.LanguageLevel = level;

                    // set total duration of the group (lead)
                    lead.TotalHours = GetTotalHoursByLevel(lead.LanguageLevel);
                }

                // set status
                var status = attendanceUow.LeadStatuses.GetByAmoId(lead.AmoStatusId);

                // if status is not found in database, this is probably the first import, so get status from import context
                // (already parsed but not committed to database yet)
                if (status == null)
                {
                    context.LeadStatusMap.TryGetValue(lead.AmoStatusId, out status);
                }

                lead.Status = status;

                var existingLead = attendanceUow.Leads.GetByAmoId(lead.AmoId);
                if (existingLead == null)
                {
                    // generate lessons according to schedule and add them to datacontext
                    foreach (var lesson in CreateLessonsForLead(lead))
                    {
                        // generate default attendance records
                        foreach (var contact in lead.Contacts)
                        {
                            var attendance = new Attendance
                            {
                                Attended = false,
                                Contact = contact,
                                Lesson = lesson
                            };

                            lesson.Attendances.Add(attendance);
                            attendanceUow.Attendances.Add(attendance);
                        }

                        attendanceUow.Lessons.Add(lesson);
                    }

                    attendanceUow.Leads.Add(lead);
                }
                else
                {
                    existingLead.CopyValuesFrom(lead);
                    attendanceUow.Leads.Update(existingLead);
                }
            }
        }

        private void ImportContacts(AmoCrmImportContext context)
        {
            foreach (var contact in context.ContactsMap.Values)
            {
                // set responsible user
                User user;
                if (context.UsersMap.TryGetValue(contact.AmoResponsibleUserId, out user))
                {
                    contact.ResponsibleUser = user;
                }
                
                var existingContact = attendanceUow.Contacts.GetByAmoId(contact.AmoId);
                if (existingContact == null)
                {
                    attendanceUow.Contacts.Add(contact);
                }
                else
                {
                    existingContact.CopyValuesFrom(contact);
                    attendanceUow.Contacts.Update(existingContact);
                }
            }
        }

        private static void SetupContactLeadLinks(AmoCrmImportContext context)
        {
            foreach (var lead in context.LeadsMap.Values)
            {
                HashSet<int> leadContactAmoIds;
                if (!context.LeadContactsMap.TryGetValue(lead.AmoId, out leadContactAmoIds))
                {
                    continue;
                }

                foreach (var contactAmoId in leadContactAmoIds)
                {
                    Contact contact;
                    if (!context.ContactsMap.TryGetValue(contactAmoId, out contact))
                    {
                        continue;
                    }

                    contact.Leads.Add(lead);
                    lead.Contacts.Add(contact);
                }
            }
        }

        private void ImportLevels(AmoCrmImportContext context)
        {
            foreach (var level in context.LevelsMap.Values)
            {
                var existingLevel = attendanceUow.Levels.GetByAmoId(level.AmoId);
                if (existingLevel == null)
                {
                    attendanceUow.Levels.Add(level);
                }
                else
                {
                    existingLevel.CopyValuesFrom(level);
                    attendanceUow.Levels.Update(existingLevel);
                }
            }
        }

        private void ImportLeadStatuses(AmoCrmImportContext context)
        {
            foreach (var status in context.LeadStatusMap.Values)
            {
                var existingStatus = attendanceUow.LeadStatuses.GetByAmoId(status.AmoId);
                if (existingStatus == null)
                {
                    attendanceUow.LeadStatuses.Add(status);
                }
                else
                {
                    existingStatus.CopyValuesFrom(status);
                    attendanceUow.LeadStatuses.Update(existingStatus);
                }
            }
        }

        private void ImportUsers(AmoCrmImportContext context)
        {
            foreach (var user in context.UsersMap.Values)
            {
                var existingUser = attendanceUow.Users.GetByAmoId(user.AmoId);
                if (existingUser == null)
                {
                    attendanceUow.Users.Add(user);
                }
                else
                {
                    existingUser.CopyValuesFrom(user);
                    attendanceUow.Users.Update(existingUser);
                }
            }
        }

        public void ClearExistingAttendanceData()
        {
            foreach (var attendance in attendanceUow.Attendances.GetAll())
            {
                attendanceUow.Attendances.Delete(attendance);
            }

            foreach (var lesson in attendanceUow.Lessons.GetAll())
            {
                attendanceUow.Lessons.Delete(lesson);
            }
            
            //foreach (var task in attendanceUow.Tasks.GetAll())
            //{
            //    attendanceUow.Tasks.Delete(task);
            //}

            foreach (var lead in attendanceUow.Leads.GetAll())
            {
                lead.Contacts.Clear();
                attendanceUow.Leads.Delete(lead);
            }

            foreach (var contact in attendanceUow.Contacts.GetAll())
            {
                contact.Leads.Clear();
                attendanceUow.Contacts.Delete(contact);
            }
            
            foreach (var level in attendanceUow.Levels.GetAll())
            {
                attendanceUow.Levels.Delete(level);
            }
            
            foreach (var user in attendanceUow.Users.GetAll())
            {
                attendanceUow.Users.Delete(user);
            }

            attendanceUow.Commit();
        }

        private IEnumerable<Lesson> CreateLessonsForLead(Lead lead)
        {
            if (!lead.StartDate.HasValue || lead.Days == null || !lead.Duration.HasValue || lead.LanguageLevel == null)
            {
                return Enumerable.Empty<Lesson>();
            }
            
            return CalculateLessonDates(lead.StartDate.Value, lead.Days, lead.Duration.Value, lead.TotalHours).Select(lessonDate => new Lesson
            {
                AcademicHours = lead.Duration.Value,
                Date = lessonDate,
                Lead = lead,
                Status = LessonStatus.Planned
            });
        }

        private int GetTotalHoursByLevel(Level languageLevel)
        {
            if (languageLevel == null)
            {
                throw new ArgumentNullException("languageLevel");
            }

            if (lowLevelSet.Contains(languageLevel.Name))
            {
                return LowLevelTotalHoursNumber;
            }

            if (highLevelSet.Contains(languageLevel.Name))
            {
                return HighLevelTotalHoursNumber;
            }

            throw new ArgumentException("Could not determine total course duration for level '" + languageLevel.Name + "'");
        }

        private static IEnumerable<DateTime> CalculateLessonDates(DateTime startDate, IEnumerable<DayOfWeek> days, int duration, int totalHours)
        {
            var lessonDates = new List<DateTime>();
            var totalHoursCount = 0;
            var date = startDate;
            var daysOfWeekMap = new HashSet<DayOfWeek>(days);
            while (totalHoursCount < totalHours)
            {
                if (daysOfWeekMap.Contains(date.DayOfWeek))
                {
                    totalHoursCount += duration;
                    lessonDates.Add(date);
                }

                date = date.AddDays(1);
            }

            var count = lessonDates.Count;
            return lessonDates;
        }

        public void Dispose()
        {
            attendanceUow.Dispose();
        }
    }
}
