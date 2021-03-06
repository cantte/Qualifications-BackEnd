using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualificationsAPI.Data;
using QualificationsAPI.Model;
using QualificationsAPI.Model.ViewModel.Activity;
using QualificationsAPI.Model.ViewModel.Subject;

namespace QualificationsAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QualificationsController : ControllerBase
    {
        private readonly ApplicationDbContext dbContext;
        private readonly IMapper _mapper;

        public QualificationsController(ApplicationDbContext context, IMapper mapper)
        {
            dbContext = context;
            _mapper = mapper;
        }

        [HttpPost("[action]")]
        public async Task<ActionResult<SubjectViewModel>> SaveSubject([FromBody] SubjectInputModel subjectInput)
        {
            Subject subject = _mapper.Map<Subject>(subjectInput);

            for (int i = 0; i < 3; ++i)
                subject.Qualifications.Add(new Qualification { Cort = i + 1 });

            dbContext.Subjects.Add(subject);
            await dbContext.SaveChangesAsync();

            return _mapper.Map<SubjectViewModel>(subject);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult<ActivityViewModel>> SaveActivity([FromBody] ActivityInputModel activityInput)
        {
            Qualification qualification = await dbContext.Qualifications.Include(a => a.Activities)
                .Where(q => q.Id == activityInput.QualificationId).FirstOrDefaultAsync();
            if (qualification is null)
                return BadRequest($"There is no Qualification with the id = {activityInput.QualificationId}");

            Activity activity = _mapper.Map<Activity>(activityInput);

            if (!qualification.AddActivity(activity))
                return BadRequest($"The activity cannot be added, the percentage of the activity exceeds the allowed");

            qualification.Calculate();
            dbContext.Qualifications.Update(qualification);
            await dbContext.SaveChangesAsync();

            return _mapper.Map<ActivityViewModel>(activity);
        }

        [HttpGet("[action]/{userId}")]
        public async Task<ActionResult<IEnumerable<SubjectViewModel>>> GetSubjects(string userId)
        {
            List<Subject> subjects = await dbContext.Subjects.Include(s => s.Qualifications).ThenInclude(s => s.Activities)
                .Where(s => s.UserId == userId).ToListAsync();
            return Ok(_mapper.Map<IEnumerable<SubjectViewModel>>(subjects));
        }

        [HttpPut("[action]/{code}")]
        public async Task<ActionResult<SubjectViewModel>> UpdateSubject(string code, [FromBody] SubjectEditModel editModel)
        {
            Subject subject = await dbContext.Subjects.FindAsync(code);
            if (subject is null)
                return NotFound($"There is not Subject with the code = {code}");

            _mapper.Map(editModel, subject);
            dbContext.Subjects.Update(subject);

            await dbContext.SaveChangesAsync();

            return _mapper.Map<SubjectViewModel>(subject);
        }

        [HttpPut("[action]/{id}")]
        public async Task<ActionResult<ActivityViewModel>> UpdateActivity(int id, [FromBody] ActivityEditModel activityEdit)
        {
            Activity activity = await dbContext.Activities.FindAsync(id);
            if (activity is null)
                return NotFound($"There is not Activity with the id = {id}");

            Qualification qualification = await dbContext.Qualifications.Include(a => a.Activities)
                .Where(q => q.Id == activity.QualificationId).FirstOrDefaultAsync();
            if (qualification is null)
                return BadRequest($"There is no Qualification with the id = {activity.QualificationId}");

            decimal diff = activityEdit.Percent - activity.Percent;
            if (qualification.TotalActivitiesPercent + diff > Qualification.MAX_PERCENT)
                return BadRequest($"The activity cannot be added, the percentage of the activity exceeds the allowed");

            _mapper.Map(activityEdit, activity);
            dbContext.Activities.Update(activity);

            qualification.Activities.RemoveAll(a => a.Id == activity.Id);
            if (!qualification.AddActivity(activity))
                return BadRequest($"The activity cannot be added, the percentage of the activity exceeds the allowed");

            qualification.Calculate();
            dbContext.Update(qualification);

            await dbContext.SaveChangesAsync();

            return _mapper.Map<ActivityViewModel>(activity);
        }

        [HttpDelete("[action]/{code}")]
        public async Task<ActionResult<SubjectViewModel>> DeleteSubject(string code)
        {
            Subject subject = await dbContext.Subjects.FindAsync(code);
            if (subject is null)
                return NotFound($"There is not Subject with the code = {code}");

            dbContext.Subjects.Remove(subject);
            await dbContext.SaveChangesAsync();

            return _mapper.Map<SubjectViewModel>(subject);
        }

        [HttpDelete("[action]/{id}")]
        public async Task<ActionResult<ActivityViewModel>> DeleteActivity(int id)
        {
            Activity activity = await dbContext.Activities.FindAsync(id);
            if (activity is null)
                return NotFound($"There is not Activity with the id = {id}");

            Qualification qualification = await dbContext.Qualifications.Include(a => a.Activities)
                .Where(q => q.Id == activity.QualificationId).FirstOrDefaultAsync();
            if (qualification is null)
                return BadRequest($"There is no Qualification with the id = {activity.QualificationId}");

            qualification.Activities.RemoveAll(a => a.Id == activity.Id);
            qualification.Calculate();

            dbContext.Activities.Remove(activity);
            dbContext.Qualifications.Update(qualification);
            await dbContext.SaveChangesAsync();

            return _mapper.Map<ActivityViewModel>(activity);
        }
    }
}
