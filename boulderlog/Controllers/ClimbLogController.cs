﻿using Boulderlog.Data;
using Boulderlog.Data.Models;
using Boulderlog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Boulderlog.Controllers
{
    [Authorize]
    public class ClimbLogController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static IEnumerable<string> Type = new List<string>() { "Attempt", "Top" };

        public ClimbLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ClimbLog
        public async Task<IActionResult> Index(int? gymId, DateTime? from, DateTime? to)
        {
            var gyms = _context.Gym.Include(x => x.Franchise).Select(x => new { x.Id, x.Name, Group = new SelectListGroup { Name = x.Franchise.Name } });

            var model = new ClimbLogViewModel()
            {
                GymId = gymId ?? 2,
                From = from ?? DateTime.Now.AddDays(-30),
                To = to ?? DateTime.Now,
                Gyms = new SelectList(gyms, "Id", "Name", null, "Group.Name"),
                SelectedGymId = gymId
            };

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var climbLogs = await _context
                .ClimbLog
                .Include(c => c.Climb)
                .Where(x => x.Climb.UserId == userId)
                .Where(x => model.From <= x.TimeStamp && x.TimeStamp <= model.To)
                .OrderBy(x => x.TimeStamp)
                .ToListAsync();

            var climbsPerDay = climbLogs.GroupBy(x => $"{x.TimeStamp.Year}-{x.TimeStamp.Month}-{x.TimeStamp.Day}", x => new { x.Type, x.ClimbId });
            foreach (var climbs in climbsPerDay)
            {
                model.SessionLabels.Add($"{climbs.Key}");
                model.SessionValuesAttempt.Add(climbs.Count(x => x.Type == "Attempt"));
                model.SessionValuesTop.Add(climbs.Count(x => x.Type == "Top"));
                model.SessionBoulders.Add(climbs.Select(x => x.ClimbId).Distinct().Count());
            }

            var grades = _context.Gym.Include(x => x.Franchise.Grade).FirstOrDefault(x => x.Id == model.GymId).Franchise.Grade;
            foreach (var grade in grades)
            {
                var logsForGrade = climbLogs.Where(x => grade.Id.Equals(x.Climb.GradeId));

                if (logsForGrade.Count() == 0)
                {
                    continue;
                }

                var tops = logsForGrade.Count(x => x.Type == "Top");
                var attempt = logsForGrade.Count(x => x.Type == "Attempt");
                var uniqueClimbs = logsForGrade.DistinctBy(x => x.ClimbId).Count();
                var totalClimbs = logsForGrade.Count();

                // Sucecss rate
                model.GradeSuccessRate_Values.Add(Math.Round(1.0 * tops / totalClimbs * 100, 2));
                model.GradeSuccessRate_Label.Add(grade.ColorName);

                // Attempt:Top ratio
                model.GradeRatioAttempt_Values.Add(Math.Round(1.0 * attempt / totalClimbs, 2));
                model.GradeRatioTop_Values.Add(Math.Round(1.0 * tops / totalClimbs, 2));
                model.GradeRatioAttempt_Label.Add(grade.ColorName);

                // Average attempts
                model.GradeAverageAttempt_Values.Add(Math.Round(1.0 * totalClimbs / uniqueClimbs, 2));
                model.GradeAverageAttempt_Label.Add(grade.ColorName);

                // Untopped
                var climbsWithoutTops = logsForGrade.GroupBy(x => x.ClimbId, x => x.Type).Count(x => !x.Any(x => x == "Top"));
                model.Untopped_Values.Add(climbsWithoutTops);
                model.Untopped_Label.Add(grade.ColorName);
            }

            return View(model);
        }

        // GET: ClimbLog/Details/5
        //public async Task<IActionResult> Details(string id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var climbLog = await _context.ClimbLog
        //        .Include(c => c.Climb)
        //        .FirstOrDefaultAsync(m => m.Id == id);
        //    if (climbLog == null)
        //    {
        //        return NotFound();
        //    }

        //    return View(climbLog);
        //}

        // GET: ClimbLog/Create
        public IActionResult Create(string climbId)
        {
            ViewData["Type"] = new SelectList(Type, "Attempt");
            ViewBag.ClimbId = climbId;
            return View(new ClimbLog() { ClimbId = climbId, Type = "Attempt" });
        }

        // POST: ClimbLog/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("TimeStamp,Type,ClimbId")] ClimbLog climbLog)
        {
            if (ModelState.IsValid)
            {
                _context.Add(climbLog);
                await _context.SaveChangesAsync();
                return RedirectToAction("Index", "Climb");
            }

            ViewData["ClimbId"] = new SelectList(_context.Climb, "Id", "Id", climbLog.ClimbId);
            return View(climbLog);
        }

        //// GET: ClimbLog/Edit/5
        //public async Task<IActionResult> Edit(string id)
        //{
        //    if (id == null)
        //    {
        //        return NotFound();
        //    }

        //    var climbLog = await _context.ClimbLog.FindAsync(id);
        //    if (climbLog == null)
        //    {
        //        return NotFound();
        //    }
        //    ViewData["ClimbId"] = new SelectList(_context.Climb, "Id", "Id", climbLog.ClimbId);
        //    return View(climbLog);
        //}

        //// POST: ClimbLog/Edit/5
        //// To protect from overposting attacks, enable the specific properties you want to bind to.
        //// For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Edit(string id, [Bind("TimeStamp,Type,ClimbId")] ClimbLog climbLog)
        //{
        //    if (id != climbLog.Id)
        //    {
        //        return NotFound();
        //    }

        //    if (ModelState.IsValid)
        //    {
        //        try
        //        {
        //            _context.Update(climbLog);
        //            await _context.SaveChangesAsync();
        //        }
        //        catch (DbUpdateConcurrencyException)
        //        {
        //            if (!ClimbLogExists(climbLog.Id))
        //            {
        //                return NotFound();
        //            }
        //            else
        //            {
        //                throw;
        //            }
        //        }
        //        return RedirectToAction(nameof(Index));
        //    }
        //    ViewData["ClimbId"] = new SelectList(_context.Climb, "Id", "Id", climbLog.ClimbId);
        //    return View(climbLog);
        //}

        // GET: ClimbLog/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var climbLog = await _context.ClimbLog
                .Include(c => c.Climb)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (climbLog == null)
            {
                return NotFound();
            }

            return View(climbLog);
        }

        // POST: ClimbLog/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var climbLog = await _context.ClimbLog.FindAsync(id);
            if (climbLog != null)
            {
                _context.ClimbLog.Remove(climbLog);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ClimbLogExists(string id)
        {
            return _context.ClimbLog.Any(e => e.Id == id);
        }
    }
}
